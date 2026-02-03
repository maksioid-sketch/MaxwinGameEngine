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

            // Visual crossfade timer
            sr.UpdateCrossFade(dtSeconds);

            // Clear one-frame latch
            anim.ClipFinishedThisFrame = false;

            // If a crossfade-freeze is active for this specific switch, hold animation time
            if (anim.CrossFadeHoldSeconds > 0f)
            {
                anim.CrossFadeHoldSeconds -= dtSeconds;
                if (anim.CrossFadeHoldSeconds < 0f) anim.CrossFadeHoldSeconds = 0f;

                // Keep sprite at current frame (no time advance)
                if (assets.TryGetAnimation(anim.ClipId, out var holdClip) && holdClip.Frames.Count > 0)
                {
                    ClampFrameIndex(anim, holdClip.Frames.Count);
                    ApplySprite(sr, holdClip, anim.FrameIndex);
                }

                anim.LastClipId = anim.ClipId;
                continue;
            }

            if (!assets.TryGetAnimation(anim.ClipId, out var clip)) continue;
            if (clip.Frames.Count == 0) continue;

            bool clipChanged = !string.Equals(anim.LastClipId, anim.ClipId, StringComparison.OrdinalIgnoreCase);

            // Compute clip length (seconds)
            float clipLen = 0f;
            for (int i = 0; i < clip.Frames.Count; i++)
                clipLen += Math.Max(0.0001f, clip.Frames[i].DurationSeconds);

            anim.ClipLengthSeconds = clipLen;

            // Reset requested by controller/logic
            if (anim.ResetRequested)
            {
                ResetToStartBasedOnDirection(anim, clip);
                anim.ResetRequested = false;
            }

            if (!anim.Playing)
            {
                ClampFrameIndex(anim, clip.Frames.Count);
                ApplySprite(sr, clip, anim.FrameIndex);
                anim.LastClipId = anim.ClipId;
                continue;
            }

            bool loop = anim.LoopOverride ? anim.Loop : clip.Loop;

            // Direction is controlled by anim.Speed sign
            float scaledDt = dtSeconds * anim.Speed; // can be negative
            anim.TimeIntoFrame += scaledDt;
            anim.ClipTimeSeconds += scaledDt;

            ClampFrameIndex(anim, clip.Frames.Count);

            if (scaledDt >= 0f)
            {
                StepForward(anim, sr, assets, clip, loop, clipChanged);
            }
            else
            {
                StepBackward(anim, sr, assets, clip, loop, clipChanged);
            }

            anim.LastClipId = anim.ClipId;
        }
    }

    private static void StepForward(
        Animator anim,
        SpriteRenderer sr,
        IAssetProvider assets,
        Engine.Core.Assets.Animation.AnimationClip clip,
        bool loop,
        bool clipChanged)
    {
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

                // Finished (hit end)
                anim.ClipFinishedThisFrame = true;
                anim.ClipTimeSeconds = anim.ClipLengthSeconds;

                if (TrySwitchToNextClip(anim, sr, assets))
                {
                    // on switch, treat it like a clip change for crossfade
                    // (TrySwitchToNextClip already applied sprite)
                    return;
                }

                // Stop on last frame
                anim.FrameIndex = clip.Frames.Count - 1;
                anim.TimeIntoFrame = 0f;
                anim.Playing = false;
                ApplySprite(sr, clip, anim.FrameIndex);
                return;
            }
        }

        ApplySprite(sr, clip, anim.FrameIndex);

        // Crossfade only when clip changes (not on frame changes)
        if (clipChanged)
            StartCrossFadeIfNeeded(anim, sr, previousSpriteId: null); // consumed safely; see function
    }

    private static void StepBackward(
        Animator anim,
        SpriteRenderer sr,
        IAssetProvider assets,
        Engine.Core.Assets.Animation.AnimationClip clip,
        bool loop,
        bool clipChanged)
    {
        // In reverse, TimeIntoFrame counts down.
        while (true)
        {
            if (anim.TimeIntoFrame >= 0f)
                break;

            // step to previous frame
            anim.FrameIndex--;

            if (anim.FrameIndex < 0)
            {
                if (loop)
                {
                    anim.FrameIndex = clip.Frames.Count - 1;
                    float lastDur = Math.Max(0.0001f, clip.Frames[anim.FrameIndex].DurationSeconds);
                    anim.TimeIntoFrame += lastDur;

                    if (anim.ClipLengthSeconds > 0f)
                    {
                        // wrap time toward end
                        anim.ClipTimeSeconds = anim.ClipLengthSeconds + (anim.ClipTimeSeconds % anim.ClipLengthSeconds);
                        if (anim.ClipTimeSeconds > anim.ClipLengthSeconds) anim.ClipTimeSeconds %= anim.ClipLengthSeconds;
                    }

                    continue;
                }

                // Finished (hit beginning)
                anim.ClipFinishedThisFrame = true;
                anim.ClipTimeSeconds = 0f;

                if (TrySwitchToNextClip(anim, sr, assets))
                {
                    return;
                }

                // Stop on first frame
                anim.FrameIndex = 0;
                anim.TimeIntoFrame = 0f;
                anim.Playing = false;
                ApplySprite(sr, clip, anim.FrameIndex);
                return;
            }

            float dur = Math.Max(0.0001f, clip.Frames[anim.FrameIndex].DurationSeconds);
            anim.TimeIntoFrame += dur;
        }

        ApplySprite(sr, clip, anim.FrameIndex);

        if (clipChanged)
            StartCrossFadeIfNeeded(anim, sr, previousSpriteId: null);
    }

    private static bool TrySwitchToNextClip(Animator anim, SpriteRenderer sr, IAssetProvider assets)
    {
        if (string.IsNullOrWhiteSpace(anim.NextClipId))
        {
            ConsumePendingFadeOverrides(anim);
            anim.CrossFadeHoldSeconds = 0f;
            return false;
        }

        if (!assets.TryGetAnimation(anim.NextClipId, out var nextClip) || nextClip.Frames.Count == 0)
        {
            ConsumePendingFadeOverrides(anim);
            anim.CrossFadeHoldSeconds = 0f;
            return false;
        }

        string prevSpriteId = sr.SpriteId;

        anim.ClipId = anim.NextClipId!;
        anim.NextClipId = null;

        // Start new clip from start/end depending on direction
        ResetToStartBasedOnDirection(anim, nextClip);

        anim.Playing = true;

        // Force sprite now
        ApplySprite(sr, nextClip, anim.FrameIndex);

        // Crossfade if sprite changed
        if (!string.IsNullOrWhiteSpace(prevSpriteId) &&
            !string.Equals(prevSpriteId, sr.SpriteId, StringComparison.OrdinalIgnoreCase))
        {
            StartCrossFadeIfNeeded(anim, sr, prevSpriteId);
        }
        else
        {
            ConsumePendingFadeOverrides(anim);
            anim.CrossFadeHoldSeconds = 0f;
        }

        // Commit pending controller state (transition clip finished)
        if (!string.IsNullOrWhiteSpace(anim.PendingStateId))
        {
            anim.StateId = anim.PendingStateId!;
            anim.PendingStateId = null;
            anim.StateTimeSeconds = 0f;
        }

        return true;
    }

    private static void ResetToStartBasedOnDirection(Animator anim, Engine.Core.Assets.Animation.AnimationClip clip)
    {
        if (anim.Speed >= 0f)
        {
            anim.FrameIndex = 0;
            anim.TimeIntoFrame = 0f;
            anim.ClipTimeSeconds = 0f;
        }
        else
        {
            anim.FrameIndex = clip.Frames.Count - 1;
            float dur = Math.Max(0.0001f, clip.Frames[anim.FrameIndex].DurationSeconds);
            // Start “inside” the last frame, so stepping backwards works immediately
            anim.TimeIntoFrame = dur;
            anim.ClipTimeSeconds = anim.ClipLengthSeconds;
        }
    }

    private static void StartCrossFadeIfNeeded(Animator anim, SpriteRenderer sr, string? previousSpriteId)
    {
        // If previousSpriteId is null, we still must consume pending overrides to prevent leaks.
        float fade = anim.PendingCrossFadeSeconds ?? anim.DefaultCrossFadeSeconds;
        bool freeze = anim.PendingFreezeDuringCrossFade ?? anim.DefaultFreezeDuringCrossFade;

        ConsumePendingFadeOverrides(anim);

        if (string.IsNullOrWhiteSpace(previousSpriteId) || fade <= 0f)
        {
            anim.CrossFadeHoldSeconds = 0f;
            return;
        }

        sr.StartCrossFade(previousSpriteId, fade);
        anim.CrossFadeHoldSeconds = freeze ? sr.CrossFadeDurationSeconds : 0f;
    }

    private static void ConsumePendingFadeOverrides(Animator anim)
    {
        anim.PendingCrossFadeSeconds = null;
        anim.PendingFreezeDuringCrossFade = null;
    }

    private static void ClampFrameIndex(Animator anim, int frameCount)
    {
        if (frameCount <= 0) { anim.FrameIndex = 0; return; }
        if (anim.FrameIndex < 0) anim.FrameIndex = 0;
        if (anim.FrameIndex >= frameCount) anim.FrameIndex = frameCount - 1;
    }

    private static void ApplySprite(SpriteRenderer sr, Engine.Core.Assets.Animation.AnimationClip clip, int frameIndex)
    {
        if (frameIndex < 0) frameIndex = 0;
        if (frameIndex >= clip.Frames.Count) frameIndex = clip.Frames.Count - 1;

        var f = clip.Frames[frameIndex];
        if (!string.IsNullOrWhiteSpace(f.SpriteId))
            sr.SpriteId = f.SpriteId;
    }
}
