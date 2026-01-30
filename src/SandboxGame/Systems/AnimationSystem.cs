using Engine.Core.Assets;
using Engine.Core.Components;
using Engine.Core.Scene;
using Engine.Core.Systems;
using System;

namespace SandboxGame.Systems;

public sealed class AnimationSystem : ISystem
{
    private readonly IAssetProvider _assets;

    public AnimationSystem(IAssetProvider assets)
    {
        _assets = assets;
    }

    public void Update(Scene scene, float dtSeconds)
    {
        foreach (var e in scene.Entities)
        {
            if (!e.TryGet<Animator>(out var anim) || anim is null) continue;
            if (!e.TryGet<SpriteRenderer>(out var sr) || sr is null) continue;
            if (!anim.Playing) continue;
            if (string.IsNullOrWhiteSpace(anim.ClipId)) continue;

            if (!_assets.TryGetAnimation(anim.ClipId, out var clip))
                continue;

            if (clip.Frames.Count == 0)
                continue;

            bool loop = anim.LoopOverride ? anim.Loop : clip.Loop;

            float t = dtSeconds * Math.Max(0f, anim.Speed);
            anim.TimeIntoFrame += t;

            // Keep frame index in range
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

            // Drive sprite from current frame
            var current = clip.Frames[anim.FrameIndex];
            if (!string.IsNullOrWhiteSpace(current.SpriteId))
                sr.SpriteId = current.SpriteId;
        }
    }
}
