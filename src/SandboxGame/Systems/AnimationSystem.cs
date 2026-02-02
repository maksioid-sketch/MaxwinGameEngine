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
        float dt = ctx.DeltaSeconds;
        IAssetProvider assets = ctx.Assets;

        foreach (var e in scene.Entities)
        {
            if (!e.TryGet<Animator>(out var anim) || anim is null) continue;
            if (!e.TryGet<SpriteRenderer>(out var sr) || sr is null) continue;
            if (string.IsNullOrWhiteSpace(anim.ClipId)) continue;

            // visual fade timer
            sr.UpdateCrossFade(dt);

            // one-frame latch cleared here
            anim.ClipFinishedThisFrame = false;

            // if we chose to freeze during fade for THIS switch, hold playback
            if (anim.CrossFadeHoldSeconds > 0f)
            {
                anim.CrossFadeHoldSeconds -= dt;
                if (anim.CrossFadeHoldSeconds < 0f) anim.CrossFadeHoldSeconds = 0f;

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

            // compute length
            float clipLen = 0f;
            for (int i = 0; i < clip.Frames.Count; i++)
                clipLen += Math.Max(0.0001f, clip.Frames[i].DurationSeconds);
            anim.ClipLengthSeconds = clipLen;

            bool loop = anim.LoopOverride ? anim.Loop : clip.Loop;

            // restart (direction-aware)
            if (anim.ResetRequested)
            {
                ResetToStartBasedOnDirection(anim, clip);
                anim.ResetRequested = false;
            }

            // not playing: still show correct frame sprite
            if (!anim.Playing)
            {
                ClampFrameIndex(anim, clip.Frames.Count);
                ApplySprite(sr, clip, anim.FrameIndex);

                // consume pending overrides so they don't leak
                ConsumePendingFadeOverrides(anim);
                anim.CrossFadeHoldSeconds = 0f;

                anim.LastClipId = anim.ClipId;
                continue;
            }

            // advance time (can be negative)
            float scaledDt = dt * anim.Speed;
            anim.TimeIntoFrame += scaledDt;
            anim.ClipTimeSeconds += scaledDt;

            ClampFrameIndex(anim, clip.Frames.Count);

            if (scaledDt >= 0f)
            {
                // forward stepping
                if (StepForward(anim, sr, assets, clip, loop))
                {
                    // we switched to NextClip inside StepForward
                    anim.LastClipId = anim.ClipId;
                    continue;
                }
            }
            else
            {
                // reverse stepping
                if (StepBackward(anim, sr, assets, clip, loop))
                {
                    // we switched to NextClip inside StepBackward
                    anim.LastClipId = anim.ClipId;
                    continue;
                }
            }

            // Apply current frame sprite and start fade on CLIP changes
            string prevSpriteId = sr.SpriteId;
            ApplySprite(sr, clip, anim.FrameIndex);

            if (clipChanged &&
                !string.IsNullOrWhiteSpace(prevSpriteId) &&
                !string.Equals(prevSpriteId, sr.SpriteId, StringComparison.OrdinalIgnoreCase))
            {
                StartCrossFadeIfNeeded(anim, sr, prevSpriteId);
            }
            else
            {
                // if clip changed but sprite didn't, don't let overrides leak
                ConsumePendingFadeOverrides(anim);
                anim.CrossFadeHoldSeconds = 0f;
            }

            anim.LastClipId = anim.ClipId;
        }
    }

    // returns true if we switched to NextClipId inside this method
    private static bool StepForward(
        Animator anim,
        SpriteRenderer sr,
        IAssetProvider assets,
        Engine.Core.Assets.Animation.AnimationClip clip,
        bool loop)
    {
        while (true)
        {
            var frame = clip.Frames[anim.FrameIndex];
            float dur = Math.Max(0.0001f, frame.DurationSeconds);

            if (anim.TimeIntoFrame < dur)
                return false;

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

                // end reached
                anim.ClipFinishedThisFrame = true;
                anim.ClipTimeSeconds = anim.ClipLengthSeconds;

                return SwitchToNextClipIfQueued(anim, sr, assets);
            }
        }
    }

    // returns true if we switched to NextClipId inside this method
    private static bool StepBackward(
        Animator anim,
        SpriteRenderer sr,
        IAssetProvider assets,
        Engine.Core.Assets.Animation.AnimationClip clip,
        bool loop)
    {
        // reverse: when time goes < 0, go to previous frame
        while (true)
        {
            if (anim.TimeIntoFrame >= 0f)
                return false;

            anim.FrameIndex--;

            if (anim.FrameIndex < 0)
            {
                if (loop)
                {
                    anim.FrameIndex = clip.Frames.Count - 1;
                    float lastDur = Math.Max(0.0001f, clip.Frames[anim.FrameIndex].DurationSeconds);
                    anim.TimeIntoFrame += lastDur;
                    continue;
                }

                // start reached
                anim.ClipFinishedThisFrame = true;
                anim.ClipTimeSeconds = 0f;

                return SwitchToNextClipIfQueued(anim, sr, assets);
            }

            float dur = Math.Max(0.0001f, clip.Frames[anim.FrameIndex].DurationSeconds);
            anim.TimeIntoFrame += dur;
        }
    }

    private static bool SwitchToNextClipIfQueued(Animator anim, SpriteRenderer sr, IAssetProvider assets)
    {
        if (string.IsNullOrWhiteSpace(anim.NextClipId))
        {
            // stop on boundary frame
            anim.Playing = false;
            anim.TimeIntoFrame = 0f;

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

        // direction-aware restart on the new clip
        ResetToStartBasedOnDirection(anim, nextClip);
        anim.Playing = true;

        // force sprite now
        ApplySprite(sr, nextClip, anim.FrameIndex);

        // Start fade if sprite changed
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

        // commit pending controller state (transition clip finished)
        if (!string.IsNullOrWhiteSpace(anim.PendingStateId))
        {
            anim.StateId = anim.PendingStateId!;
            anim.PendingStateId = null;
            anim.StateTimeSeconds = 0f;
        }

        return true;
    }

    private static void StartCrossFadeIfNeeded(Animator anim, SpriteRenderer sr, string prevSpriteId)
    {
        float fade = anim.PendingCrossFadeSeconds ?? anim.DefaultCrossFadeSeconds;
        bool freeze = anim.PendingFreezeDuringCrossFade ?? anim.DefaultFreezeDuringCrossFade;

        // consume one-shot overrides (critical so they don't stick)
        ConsumePendingFadeOverrides(anim);

        if (fade <= 0f)
        {
            anim.CrossFadeHoldSeconds = 0f;
            return;
        }

        sr.StartCrossFade(prevSpriteId, fade);
        anim.CrossFadeHoldSeconds = freeze ? sr.CrossFadeDurationSeconds : 0f;
    }

    private static void ConsumePendingFadeOverrides(Animator anim)
    {
        anim.PendingCrossFadeSeconds = null;
        anim.PendingFreezeDuringCrossFade = null;
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
            anim.TimeIntoFrame = dur; // start inside last frame for reverse
            anim.ClipTimeSeconds = anim.ClipLengthSeconds;
        }
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
