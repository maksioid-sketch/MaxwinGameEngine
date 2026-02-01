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
            if (!anim.Playing) continue;
            if (string.IsNullOrWhiteSpace(anim.ClipId)) continue;

            if (!assets.TryGetAnimation(anim.ClipId, out var clip)) continue;
            if (clip.Frames.Count == 0) continue;

            if (anim.ResetRequested)
            {
                anim.FrameIndex = 0;
                anim.TimeIntoFrame = 0f;
                anim.ResetRequested = false;
            }

            bool loop = anim.LoopOverride ? anim.Loop : clip.Loop;

            float t = dtSeconds * System.Math.Max(0f, anim.Speed);
            anim.TimeIntoFrame += t;

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
                        continue;
                    }

                    // Non-looping clip ended
                    if (!string.IsNullOrWhiteSpace(anim.NextClipId) &&
                        assets.TryGetAnimation(anim.NextClipId, out var nextClip) &&
                        nextClip.Frames.Count > 0)
                    {
                        anim.ClipId = anim.NextClipId!;
                        anim.NextClipId = null;

                        anim.FrameIndex = 0;
                        anim.TimeIntoFrame = 0f;
                        anim.Playing = true;

                        // Force sprite to the first frame of the next clip NOW
                        var nf = nextClip.Frames[0];
                        if (!string.IsNullOrWhiteSpace(nf.SpriteId))
                            sr.SpriteId = nf.SpriteId;

                        if (!string.IsNullOrWhiteSpace(anim.PendingStateId))
                        {
                            anim.StateId = anim.PendingStateId!;
                            anim.PendingStateId = null;
                        }

                        // IMPORTANT: stop processing leftovers for THIS ENTITY only
                        goto NextEntity; // or use continue with a flag; see below
                    }
                    else
                    {
                        // No next clip: freeze on last valid frame
                        anim.FrameIndex = clip.Frames.Count - 1;
                        anim.TimeIntoFrame = 0f;
                        anim.Playing = false;
                        goto NextEntity;
                    }
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
