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

            if (!assets.TryGetAnimation(anim.ClipId, out var clip))
                continue;

            if (clip.Frames.Count == 0)
                continue;

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
                    }
                    else
                    {
                        anim.FrameIndex = clip.Frames.Count - 1;
                        anim.Playing = false;
                        anim.TimeIntoFrame = 0f;
                        break;
                    }
                }
            }

            var current = clip.Frames[anim.FrameIndex];
            if (!string.IsNullOrWhiteSpace(current.SpriteId))
                sr.SpriteId = current.SpriteId;
        }
    }
}
