using Engine.Core.Assets;
using Engine.Core.Assets.Animation;
using Engine.Core.Components;
using Engine.Core.Platform.Input;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;
using System;

namespace SandboxGame.Systems;

public sealed class AnimatorControllerSystem : ISystem
{
    private readonly Func<IAssetProvider> _assets;

    public AnimatorControllerSystem(Func<IAssetProvider> assetsAccessor)
    {
        _assets = assetsAccessor;
    }

    public void Update(Scene scene, EngineContext ctx)
    {
        var assets = _assets();

        foreach (var e in scene.Entities)
        {
            if (!e.TryGet<Animator>(out var anim) || anim is null) continue;
            if (string.IsNullOrWhiteSpace(anim.ControllerId)) continue;

            if (!assets.TryGetController(anim.ControllerId, out var controller)) continue;
            if (controller.States.Count == 0) continue;

            // If a transition is in progress, do not override it (no interruptions yet)
            if (!string.IsNullOrWhiteSpace(anim.PendingStateId))
                continue;

            // Initialize on first run
            if (string.IsNullOrWhiteSpace(anim.StateId))
            {
                anim.StateId = controller.InitialState;

                if (controller.States.TryGetValue(anim.StateId, out var st) && !string.IsNullOrWhiteSpace(st.ClipId))
                {
                    SetClip(anim, st.ClipId, restart: true);
                }

                continue;
            }

            // Evaluate transitions in order (first match wins)
            for (int i = 0; i < controller.Transitions.Count; i++)
            {
                var t = controller.Transitions[i];

                if (!t.From.Equals(anim.StateId, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!Matches(t.When, ctx.Input))
                    continue;

                if (!controller.States.TryGetValue(t.To, out var toState) || string.IsNullOrWhiteSpace(toState.ClipId))
                    continue;

                // ---------- TRANSITION CLIP ----------
                if (!string.IsNullOrWhiteSpace(t.TransitionClipId))
                {
                    // Prevent retriggering the same transition clip repeatedly
                    if (string.Equals(anim.ClipId, t.TransitionClipId, StringComparison.OrdinalIgnoreCase) && anim.Playing)
                        break;

                    anim.PendingStateId = t.To;
                    anim.NextClipId = toState.ClipId;

                    SetClip(anim, t.TransitionClipId!, restart: true);
                    break;
                }

                // ---------- DIRECT SWITCH ----------
                // Commit state immediately
                anim.StateId = t.To;

                // Clear any stale "transition" bookkeeping
                anim.PendingStateId = null;
                anim.NextClipId = null;

                // Only restart when the clip actually changes
                SetClip(anim, toState.ClipId, restart: false);
                break;
            }
        }
    }

    private static void SetClip(Animator anim, string clipId, bool restart)
    {
        if (string.IsNullOrWhiteSpace(clipId))
            return;

        // If clip changes -> restart. If same clip -> only restart if requested.
        bool changed = !string.Equals(anim.ClipId, clipId, StringComparison.OrdinalIgnoreCase);

        if (changed)
        {
            anim.ClipId = clipId;
            anim.Playing = true;
            anim.ResetRequested = true;
            return;
        }

        // Same clip:
        anim.Playing = true;

        // Only restart if explicitly asked (usually false for idle/walk, true for transitions)
        if (restart)
            anim.ResetRequested = true;
    }

    private static bool Matches(TransitionWhen w, IInput input)
    {
        if (w.PressedAny is { Length: > 0 })
        {
            bool any = false;
            for (int i = 0; i < w.PressedAny.Length; i++)
                any |= WasPressed(input, w.PressedAny[i]);
            if (!any) return false;
        }

        if (w.DownAny is { Length: > 0 })
        {
            bool any = false;
            for (int i = 0; i < w.DownAny.Length; i++)
                any |= IsDown(input, w.DownAny[i]);
            if (!any) return false;
        }

        if (w.NoneDown is { Length: > 0 })
        {
            for (int i = 0; i < w.NoneDown.Length; i++)
                if (IsDown(input, w.NoneDown[i])) return false;
        }

        return true;
    }

    private static bool WasPressed(IInput input, string keyName)
        => TryParseKey(keyName, out var k) && input.WasPressed(k);

    private static bool IsDown(IInput input, string keyName)
        => TryParseKey(keyName, out var k) && input.IsDown(k);

    private static bool TryParseKey(string name, out InputKey key)
        => Enum.TryParse(name, ignoreCase: true, out key);
}
