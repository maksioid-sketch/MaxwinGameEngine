using System;
using Engine.Core.Assets;
using Engine.Core.Components;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;

namespace SandboxGame.Systems;

public sealed class AnimationSystem : ISystem
{
    public void Update(Scene scene, EngineContext ctx)
    {
        float dtSeconds = ctx.DeltaSeconds;
        IAssetProvider assets = ctx.Assets;

        foreach (var e in scene.Entities)
        {
            if (!e.TryGet<Animator>(out var anim) || anim is null) continue;
            if (!e.TryGet<SpriteRenderer>(out var sr) || sr is null) continue;
            if (string.IsNullOrWhiteSpace(anim.ClipId)) continue;

            // advance crossfade timer (purely visual)
            sr.UpdateCrossFade(dtSeconds);

            // one-frame latch cleared each update (set to true when a non-looping clip ends)
            anim.ClipFinishedThisFrame = false;

            // If a crossfade-freeze is active for this specific switch, hold animation time
            if (anim.CrossFadeHoldSeconds > 0f)
            {
                anim.CrossFadeHoldSeconds -= dtSeconds;
                if (anim.CrossFadeHoldSeconds < 0f) anim.CrossFadeHoldSeconds = 0f;

                // Keep sprite at current frame (no time advance)
                if (assets.TryGetAnimation(anim.ClipId, out var holdClip) && holdClip.Frames.Count > 0)
                {
                    int idx = anim.FrameIndex;
                    if (idx < 0) idx = 0;
                    if (idx >= holdClip.Frames.Count) idx = holdClip.Frames.Count - 1;

                    var f = holdClip.Frames[idx];
                    if (!string.IsNullOrWhiteSpace(f.SpriteId))
                        sr.SpriteId = f.SpriteId;
                }

                anim.LastClipId = anim.ClipId;
                continue;
            }

            if (!assets.TryGetAnimation(anim.ClipId, out var clip)) continue;
            if (clip.Frames.Count == 0) continue;

            // Detect clip change (crossfade only on clip switches, not frame changes)
            bool clipChanged = !string.Equals(anim.LastClipId, anim.ClipId, StringComparison.OrdinalIgnoreCase);

            // Compute clip length (seconds)
            float clipLen = 0f;
            for (int i = 0; i < clip.Frames.Count; i++)
                clipLen += Math.Max(0.0001f, clip.Frames[i].DurationSeconds);

            anim.ClipLengthSeconds = clipLen;

            // Reset requested by controller/logic
            if (anim.ResetRequested)
            {
                anim.FrameIndex = 0;
                anim.TimeIntoFrame = 0f;
                anim.ClipTimeSeconds = 0f;
                anim.ResetRequested = false;
            }

            if (!anim.Playing)
            {
                anim.LastClipId = anim.ClipId;
                continue;
            }

            bool loop = anim.LoopOverride ? anim.Loop : clip.Loop;

            float scaledDt = dtSeconds * Math.Max(0f, anim.Speed);
            anim.TimeIntoFrame += scaledDt;
            anim.ClipTimeSeconds += scaledDt;

            if (anim.FrameIndex < 0) anim.FrameIndex = 0;
            if (anim.FrameIndex >= clip.Frames.Count) anim.FrameIndex = clip.Frames.Count - 1;

            while (true)
            {
                var frame = clip.Frames[anim.FrameIndex];
                float dur = Math.Max(0.0001f, frame.DurationSeconds);

                if (anim.TimeIntoFrame < dur)
                    break;

                anim.TimeIntoFrame -= dur;
                anim.FrameIndex++;

                if (anim.FrameIndex >= clip.Frames.Count)
                {
                    if (loop)
                    {
                        anim.FrameIndex = 0;

                        if (anim.ClipLengthSeconds > 0f)
                            anim.ClipTimeSeconds %= anim.ClipLengthSeconds;

                        continue;
                    }

                    // Non-looping clip ended THIS FRAME
                    anim.ClipFinishedThisFrame = true;
                    anim.ClipTimeSeconds = anim.ClipLengthSeconds;

                    // If there is a queued next clip (transition-clip behavior), switch now
                    if (!string.IsNullOrWhiteSpace(anim.NextClipId) &&
                        assets.TryGetAnimation(anim.NextClipId, out var nextClip) &&
                        nextClip.Frames.Count > 0)
                    {
                        string prevSpriteId = sr.SpriteId;

                        anim.ClipId = anim.NextClipId!;
                        anim.NextClipId = null;

                        anim.FrameIndex = 0;
                        anim.TimeIntoFrame = 0f;
                        anim.ClipTimeSeconds = 0f;
                        anim.Playing = true;

                        // Apply first frame sprite of next clip immediately
                        var nf = nextClip.Frames[0];
                        if (!string.IsNullOrWhiteSpace(nf.SpriteId))
                        {
                            sr.SpriteId = nf.SpriteId;

                            StartCrossFadeIfNeeded(anim, sr, prevSpriteId);
                        }
                        else
                        {
                            // Even if sprite id is empty, consume pending overrides so they don't leak
                            ConsumePendingFadeOverrides(anim);
                            anim.CrossFadeHoldSeconds = 0f;
                        }

                        // Commit pending controller state
                        if (!string.IsNullOrWhiteSpace(anim.PendingStateId))
                        {
                            anim.StateId = anim.PendingStateId!;
                            anim.PendingStateId = null;
                            anim.StateTimeSeconds = 0f;
                        }

                        anim.LastClipId = anim.ClipId;
                        goto NextEntity;
                    }

                    // No next clip: freeze on last frame and stop playing
                    anim.FrameIndex = clip.Frames.Count - 1;
                    anim.TimeIntoFrame = 0f;
                    anim.Playing = false;

                    anim.LastClipId = anim.ClipId;
                    goto NextEntity;
                }
            }

            // Apply current frame sprite
            var current = clip.Frames[anim.FrameIndex];
            if (!string.IsNullOrWhiteSpace(current.SpriteId))
            {
                string prevSpriteId = sr.SpriteId;
                sr.SpriteId = current.SpriteId;

                // Crossfade only when the clip changes
                if (clipChanged &&
                    !string.IsNullOrWhiteSpace(prevSpriteId) &&
                    !string.Equals(prevSpriteId, sr.SpriteId, StringComparison.OrdinalIgnoreCase))
                {
                    StartCrossFadeIfNeeded(anim, sr, prevSpriteId);
                }
                else
                {
                    // No crossfade started this tick -> consume pending overrides so they don't leak
                    // (this matters if a transition wanted a fade but sprite id didn't actually change)
                    ConsumePendingFadeOverrides(anim);
                    anim.CrossFadeHoldSeconds = 0f;
                }
            }
            else
            {
                // No sprite id, still consume pending overrides
                ConsumePendingFadeOverrides(anim);
                anim.CrossFadeHoldSeconds = 0f;
            }

            anim.LastClipId = anim.ClipId;

        NextEntity:
            continue;
        }
    }

    private static void StartCrossFadeIfNeeded(Animator anim, SpriteRenderer sr, string prevSpriteId)
    {
        // Resolve effective fade + freeze for THIS switch
        float fade = anim.PendingCrossFadeSeconds ?? anim.DefaultCrossFadeSeconds;
        bool freeze = anim.PendingFreezeDuringCrossFade ?? anim.DefaultFreezeDuringCrossFade;

        // Consume one-shot overrides (critical so they don't persist)
        ConsumePendingFadeOverrides(anim);

        if (!string.IsNullOrWhiteSpace(prevSpriteId) &&
            !string.Equals(prevSpriteId, sr.SpriteId, StringComparison.OrdinalIgnoreCase) &&
            fade > 0f)
        {
            sr.StartCrossFade(prevSpriteId, fade);
            anim.CrossFadeHoldSeconds = freeze ? sr.CrossFadeDurationSeconds : 0f;
        }
        else
        {
            anim.CrossFadeHoldSeconds = 0f;
        }
    }

    private static void ConsumePendingFadeOverrides(Animator anim)
    {
        anim.PendingCrossFadeSeconds = null;
        anim.PendingFreezeDuringCrossFade = null;
    }
}
