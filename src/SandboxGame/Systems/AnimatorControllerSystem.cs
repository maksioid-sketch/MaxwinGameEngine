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

            // Track time spent in current controller state
            if (!string.IsNullOrWhiteSpace(anim.StateId))
                anim.StateTimeSeconds += ctx.DeltaSeconds;

            bool inTransitionClip = !string.IsNullOrWhiteSpace(anim.PendingStateId);

            // Initialize controller state
            if (string.IsNullOrWhiteSpace(anim.StateId))
            {
                anim.StateId = controller.InitialState;
                anim.StateTimeSeconds = 0f;

                if (controller.States.TryGetValue(anim.StateId, out var st) &&
                    !string.IsNullOrWhiteSpace(st.ClipId))
                {
                    SetClip(anim, st.ClipId, restart: true, speed: st.Speed);
                }

                continue;
            }

            // Keep speed synced to active state's speed (only when NOT playing a transition clip)
            if (!inTransitionClip && controller.States.TryGetValue(anim.StateId, out var currentState))
            {
                float mul = anim.GetFloat("speedMul", 1f);
                anim.Speed = currentState.Speed * mul; // allow negative for reverse
            }

            // Build candidate transitions (from current state + AnyState '*')
            var candidates = new List<(int idx, ControllerTransition t)>(controller.Transitions.Count);

            for (int i = 0; i < controller.Transitions.Count; i++)
            {
                var t = controller.Transitions[i];

                bool fromMatches =
                    t.From.Equals("*", StringComparison.OrdinalIgnoreCase) ||
                    t.From.Equals(anim.StateId, StringComparison.OrdinalIgnoreCase);

                if (!fromMatches) continue;

                // If we're currently in a transition clip, only allow interrupts
                if (inTransitionClip && !t.CanInterrupt) continue;

                // Min time in the CURRENT controller state
                if (t.MinTimeInState > 0f && anim.StateTimeSeconds < t.MinTimeInState)
                    continue;

                // Optional exit time (normalized 0..1)
                if (t.ExitTime >= 0f && anim.NormalizedTime < t.ExitTime)
                    continue;

                candidates.Add((i, t));
            }

            // Sort by priority desc, stable by JSON order (idx asc)
            candidates.Sort((a, b) =>
            {
                int pr = b.t.Priority.CompareTo(a.t.Priority);
                return pr != 0 ? pr : a.idx.CompareTo(b.idx);
            });

            // Evaluate candidates
            for (int ci = 0; ci < candidates.Count; ci++)
            {
                var t = candidates[ci].t;

                if (!controller.States.TryGetValue(t.To, out var toState) || string.IsNullOrWhiteSpace(toState.ClipId))
                    continue;

                if (!TransitionMatches(t, anim, ctx.Input))
                    continue;

                // Consume triggers used by this transition
                ConsumeTriggersUsedByTransition(t, anim);

                // One-shot overrides for the NEXT clip switch only
                anim.PendingCrossFadeSeconds = (t.CrossFadeSeconds >= 0f) ? t.CrossFadeSeconds : null;
                anim.PendingFreezeDuringCrossFade = t.FreezeDuringCrossFade; // null means use default

                // --- Transition clip path ---
                if (!string.IsNullOrWhiteSpace(t.TransitionClipId))
                {
                    // If already playing the same transition clip, do nothing
                    if (string.Equals(anim.ClipId, t.TransitionClipId, StringComparison.OrdinalIgnoreCase) && anim.Playing)
                        break;

                    // Queue destination and play transition clip now
                    anim.PendingStateId = t.To;
                    anim.NextClipId = toState.ClipId;

                    // IMPORTANT: restart transition clip so it plays from start/end (depending on speed sign)
                    SetClip(anim, t.TransitionClipId!, restart: true, speed: t.TransitionSpeed);

                    // Reset state timer so minTimeInState doesn't instantly allow flip-flop via AnyState rules
                    anim.StateTimeSeconds = 0f;

                    break;
                }

                // --- Direct switch path ---
                anim.StateId = t.To;
                anim.StateTimeSeconds = 0f;

                anim.PendingStateId = null;
                anim.NextClipId = null;

                float mul = anim.GetFloat("speedMul", 1f);

                // restart true is usually correct on state change
                SetClip(anim, toState.ClipId, restart: true, speed: toState.Speed * mul);

                break;
            }

            // DO NOT clear ClipFinishedThisFrame here.
            // AnimationSystem owns setting it and it must persist for one full frame
            // so { "finished": true } conditions can be observed.
        }
    }

    private static void SetClip(Animator anim, string clipId, bool restart, float speed)
    {
        if (string.IsNullOrWhiteSpace(clipId))
            return;

        // allow negative speed for reverse playback
        anim.Speed = speed;

        bool changed = !string.Equals(anim.ClipId, clipId, StringComparison.OrdinalIgnoreCase);

        if (changed)
        {
            anim.ClipId = clipId;
            anim.Playing = true;

            // Direction-aware restart is handled in AnimationSystem using ResetRequested + Speed sign
            anim.ResetRequested = true;

            // reset timing
            anim.ClipTimeSeconds = 0f;

            // Do NOT touch ClipFinishedThisFrame here; AnimationSystem sets/clears it.
            return;
        }

        anim.Playing = true;

        if (restart)
        {
            anim.ResetRequested = true;
            anim.ClipTimeSeconds = 0f;
        }
    }

    private static bool TransitionMatches(ControllerTransition t, Animator anim, IInput input)
    {
        if (t.Conditions is { Count: > 0 })
        {
            for (int i = 0; i < t.Conditions.Count; i++)
                if (!ConditionMet(t.Conditions[i], anim, input))
                    return false;

            return true;
        }

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
        if (c.Finished.HasValue)
            return c.Finished.Value ? anim.ClipFinishedThisFrame : !anim.ClipFinishedThisFrame;

        if (!string.IsNullOrWhiteSpace(c.Trigger))
            return anim.HasTrigger(c.Trigger!);

        if (!string.IsNullOrWhiteSpace(c.Bool))
            return anim.GetBool(c.Bool!) == c.BoolValue;

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

        return true;
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
