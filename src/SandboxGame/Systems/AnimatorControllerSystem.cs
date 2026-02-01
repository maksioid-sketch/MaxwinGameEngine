using Engine.Core.Assets;
using Engine.Core.Assets.Animation;
using Engine.Core.Components;
using Engine.Core.Platform.Input;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;
using System;
using System.Collections.Generic;

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

            // Initialize on first run
            if (string.IsNullOrWhiteSpace(anim.StateId))
            {
                anim.StateId = controller.InitialState;

                if (controller.States.TryGetValue(anim.StateId, out var st) && !string.IsNullOrWhiteSpace(st.ClipId))
                {
                    SetClip(anim, st.ClipId, restart: true, speed: st.Speed);
                }

                continue;
            }

            // If a transition clip is playing, normally do not override it.
            // But allow interrupts for transitions explicitly marked CanInterrupt=true.
            bool inTransition = !string.IsNullOrWhiteSpace(anim.PendingStateId);

            // Keep Animator.Speed in sync with the ACTIVE state when not in a transition clip.
            // This fixes the "transition speed leaked into next state" problem.
            if (!inTransition && controller.States.TryGetValue(anim.StateId, out var currentState))
            {
                float mul = anim.GetFloat("speedMul", 1f); // optional param (default 1)
                anim.Speed = MathF.Max(0f, currentState.Speed * mul);
            }

            // Build candidate transitions:
            // - From current state
            // - From "*" (Any State)
            // Priority: higher first, then stable list order
            var candidates = new List<(int index, ControllerTransition t)>(controller.Transitions.Count);
            for (int i = 0; i < controller.Transitions.Count; i++)
            {
                var t = controller.Transitions[i];

                bool fromMatches =
                    t.From.Equals("*", StringComparison.OrdinalIgnoreCase) ||
                    t.From.Equals(anim.StateId, StringComparison.OrdinalIgnoreCase);

                if (!fromMatches) continue;

                if (inTransition && !t.CanInterrupt) continue;

                candidates.Add((i, t));
            }

            candidates.Sort((a, b) =>
            {
                int pr = b.t.Priority.CompareTo(a.t.Priority);
                return pr != 0 ? pr : a.index.CompareTo(b.index);
            });

            // Evaluate transitions (first match wins)
            foreach (var (_, t) in candidates)
            {
                if (!controller.States.TryGetValue(t.To, out var toState) || string.IsNullOrWhiteSpace(toState.ClipId))
                    continue;

                if (!TransitionMatches(t, anim, ctx.Input))
                    continue;

                // If this transition uses a trigger condition, consume it now (on TAKE).
                ConsumeTriggersUsedByTransition(t, anim);

                // ---------- TRANSITION CLIP ----------
                if (!string.IsNullOrWhiteSpace(t.TransitionClipId))
                {
                    // Prevent retriggering the same transition clip repeatedly
                    if (string.Equals(anim.ClipId, t.TransitionClipId, StringComparison.OrdinalIgnoreCase) && anim.Playing)
                        break;

                    anim.PendingStateId = t.To;
                    anim.NextClipId = toState.ClipId;

                    SetClip(anim, t.TransitionClipId!, restart: true, speed: t.TransitionSpeed);
                    break;
                }

                // ---------- DIRECT SWITCH ----------
                anim.StateId = t.To;

                // Clear any stale "transition" bookkeeping
                anim.PendingStateId = null;
                anim.NextClipId = null;

                // Apply state speed now
                float mul = anim.GetFloat("speedMul", 1f);
                SetClip(anim, toState.ClipId, restart: false, speed: MathF.Max(0f, toState.Speed * mul));
                break;
            }
        }
    }

    private static void SetClip(Animator anim, string clipId, bool restart, float speed)
    {
        if (string.IsNullOrWhiteSpace(clipId))
            return;

        // Apply speed (used by AnimationSystem)
        anim.Speed = MathF.Max(0f, speed);

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

    private static bool TransitionMatches(ControllerTransition t, Animator anim, IInput input)
    {
        // New flexible conditions (v2)
        if (t.Conditions is { Count: > 0 })
        {
            for (int i = 0; i < t.Conditions.Count; i++)
            {
                if (!ConditionMet(t.Conditions[i], anim, input))
                    return false;
            }
            return true;
        }

        // Legacy (v1) input-only "when"
        return Matches(t.When, input);
    }

    private static void ConsumeTriggersUsedByTransition(ControllerTransition t, Animator anim)
    {
        if (t.Conditions is not { Count: > 0 }) return;

        for (int i = 0; i < t.Conditions.Count; i++)
        {
            var c = t.Conditions[i];
            if (!string.IsNullOrWhiteSpace(c.Trigger))
                anim.ConsumeTrigger(c.Trigger!);
        }
    }

    private static bool ConditionMet(TransitionCondition c, Animator anim, IInput input)
    {
        // Trigger presence check (consumed on take)
        if (!string.IsNullOrWhiteSpace(c.Trigger))
            return anim.HasTrigger(c.Trigger!);

        // Bool param
        if (!string.IsNullOrWhiteSpace(c.Bool))
            return anim.GetBool(c.Bool!) == c.BoolValue;

        // Float param compare
        if (!string.IsNullOrWhiteSpace(c.Float))
        {
            float v = anim.GetFloat(c.Float!);
            return c.Op switch
            {
                CompareOp.Eq => v == c.Value,
                CompareOp.Ne => v != c.Value,
                CompareOp.Gt => v > c.Value,
                CompareOp.Ge => v >= c.Value,
                CompareOp.Lt => v < c.Value,
                CompareOp.Le => v <= c.Value,
                _ => false
            };
        }

        // Input checks (optional)
        if (c.PressedAny is { Length: > 0 })
        {
            bool any = false;
            for (int i = 0; i < c.PressedAny.Length; i++)
                any |= WasPressed(input, c.PressedAny[i]);
            if (!any) return false;
        }

        if (c.DownAny is { Length: > 0 })
        {
            bool any = false;
            for (int i = 0; i < c.DownAny.Length; i++)
                any |= IsDown(input, c.DownAny[i]);
            if (!any) return false;
        }

        if (c.NoneDown is { Length: > 0 })
        {
            for (int i = 0; i < c.NoneDown.Length; i++)
                if (IsDown(input, c.NoneDown[i])) return false;
        }

        return true; // empty condition == always true
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
