using System;
using Engine.Core.Assets;
using Engine.Core.Assets.Animation;
using Engine.Core.Components;
using Engine.Core.Runtime;
using Engine.Core.Runtime.Events;
using Engine.Core.Scene;
using Engine.Core.Systems;

namespace Engine.Core.Systems.BuiltIn;

public sealed class AnimationNotifierSystem : ISystem
{
    public void Update(Scene.Scene scene, EngineContext ctx)
    {

       

        IAssetProvider assets = ctx.Assets;

        foreach (var e in scene.Entities)
        {
            if (!e.TryGet<Animator>(out var anim) || anim is null) continue;
            if (string.IsNullOrWhiteSpace(anim.ClipId)) continue;

            if (!assets.TryGetAnimation(anim.ClipId, out var clip)) continue;
            if (clip.Frames.Count == 0) continue;

            int cur = anim.FrameIndex;
            if (cur < 0) cur = 0;
            if (cur >= clip.Frames.Count) cur = clip.Frames.Count - 1;

            bool clipChanged = !string.Equals(anim.LastNotifyClipId, anim.ClipId, StringComparison.OrdinalIgnoreCase);

            if (clipChanged)
            {
                anim.LastNotifyClipId = anim.ClipId;
                anim.LastNotifyFrameIndex = cur;

                FireFrameEvents(ctx, e.Id, e.Name, anim.ClipId, cur, clip.Frames[cur].Events);
                continue;
            }

            int prev = anim.LastNotifyFrameIndex;

            // First time ever
            if (prev < 0)
            {
                anim.LastNotifyFrameIndex = cur;
                FireFrameEvents(ctx, e.Id, e.Name, anim.ClipId, cur, clip.Frames[cur].Events);
                continue;
            }

            if (prev == cur)
                continue;

            // If we jumped weirdly (reset, non-loop boundary, etc.), just fire current frame
            if (System.Math.Abs(cur - prev) > clip.Frames.Count)
            {
                anim.LastNotifyFrameIndex = cur;
                FireFrameEvents(ctx, e.Id, e.Name, anim.ClipId, cur, clip.Frames[cur].Events);
                continue;
            }

            // Try to fire all intermediate frames in the direction of playback.
            // This helps when dt is large and AnimationSystem skips frames.
            int dir = anim.Speed >= 0f ? +1 : -1;

            int i = prev;
            int steps = 0;
            int maxSteps = clip.Frames.Count; // safety

            while (i != cur && steps++ < maxSteps)
            {
                i += dir;

                // Wrap (best-effort). Works fine for looping clips, and is still safe for non-looping.
                if (i < 0) i = clip.Frames.Count - 1;
                if (i >= clip.Frames.Count) i = 0;

                FireFrameEvents(ctx, e.Id, e.Name, anim.ClipId, i, clip.Frames[i].Events);
            }

            anim.LastNotifyFrameIndex = cur;
        }
    }

    private static void FireFrameEvents(EngineContext ctx, Guid entityId, string entityName, string clipId, int frameIndex, string[] events)
    {
        if (events is null || events.Length == 0) return;

        for (int k = 0; k < events.Length; k++)
        {
            var name = events[k];
            if (string.IsNullOrWhiteSpace(name)) continue;

            ctx.Events.Publish(new AnimationNotifyEvent(entityId, entityName, clipId, frameIndex, name));
        }
    }
}
