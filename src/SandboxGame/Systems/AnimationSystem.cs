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

            if (!assets.TryGetAnimation(anim.ClipId, out var clip)) continue;
            if (clip.Frames.Count == 0) continue;

            // Compute clip length (seconds)
            float clipLen = 0f;
            for (int i = 0; i < clip.Frames.Count; i++)
                clipLen += System.Math.Max(0.0001f, clip.Frames[i].DurationSeconds);

            anim.ClipLengthSeconds = clipLen;

            if (anim.ResetRequested)
            {
                anim.FrameIndex = 0;
                anim.TimeIntoFrame = 0f;
                anim.ClipTimeSeconds = 0f;
                anim.ResetRequested = false;
            }

            if (!anim.Playing)
                continue;

            bool loop = anim.LoopOverride ? anim.Loop : clip.Loop;

            float t = dtSeconds * System.Math.Max(0f, anim.Speed);
            anim.TimeIntoFrame += t;
            anim.ClipTimeSeconds += t;

            if (anim.FrameIndex < 0) anim.FrameIndex = 0;
            if (anim.FrameIndex >= clip.Frames.Count) anim.FrameIndex = clip.Frames.Count - 1;

            while (true)
            {
                var frame = clip.Frames[anim.FrameIndex];
                float dur = System.Math.Max(0.0001f, frame.DurationSeconds);

                if (anim.TimeIntoFrame < dur)
                    break;

                anim.TimeIntoFrame -= dur;
                anim.FrameIndex++;

                if (anim.FrameIndex >= clip.Frames.Count)
                {
                    if (loop)
                    {
                        anim.FrameIndex = 0;

                        // Wrap time in looping clips
                        if (anim.ClipLengthSeconds > 0f)
                            anim.ClipTimeSeconds %= anim.ClipLengthSeconds;

                        continue;
                    }

                    // Non-looping clip ended THIS FRAME
                    anim.ClipFinishedThisFrame = true;
                    anim.ClipTimeSeconds = anim.ClipLengthSeconds;

                    if (!string.IsNullOrWhiteSpace(anim.NextClipId) &&
                        assets.TryGetAnimation(anim.NextClipId, out var nextClip) &&
                        nextClip.Frames.Count > 0)
                    {
                        // Switch to next clip immediately
                        anim.ClipId = anim.NextClipId!;
                        anim.NextClipId = null;

                        anim.FrameIndex = 0;
                        anim.TimeIntoFrame = 0f;
                        anim.ClipTimeSeconds = 0f;

                        anim.Playing = true;

                        // Force sprite to the first frame of the next clip now
                        var nf = nextClip.Frames[0];
                        if (!string.IsNullOrWhiteSpace(nf.SpriteId))
                            sr.SpriteId = nf.SpriteId;

                        // Commit pending state (controller transition finished)
                        if (!string.IsNullOrWhiteSpace(anim.PendingStateId))
                        {
                            anim.StateId = anim.PendingStateId!;
                            anim.PendingStateId = null;

                            // NEW: reset time-in-state on state commit
                            anim.StateTimeSeconds = 0f;
                        }

                        goto NextEntity;
                    }

                    // No next clip: freeze on last frame and stop playing
                    anim.FrameIndex = clip.Frames.Count - 1;
                    anim.TimeIntoFrame = 0f;
                    anim.Playing = false;
                    goto NextEntity;
                }
            }

            var current = clip.Frames[anim.FrameIndex];
            if (!string.IsNullOrWhiteSpace(current.SpriteId))
                sr.SpriteId = current.SpriteId;

            NextEntity:
            continue;
        }
    }
}
