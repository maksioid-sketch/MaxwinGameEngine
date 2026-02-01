using Engine.Core.Components;

namespace Engine.Core.Animation;

public static class AnimatorExtensions
{
    public static void Play(this Animator anim, string clipId, bool restart = true)
    {
        if (!string.Equals(anim.ClipId, clipId, StringComparison.OrdinalIgnoreCase))
            anim.ClipId = clipId;

        anim.Playing = true;

        if (restart)
            anim.ResetRequested = true;

        anim.NextClipId = null;
    }

    public static void PlayThen(this Animator anim, string clipId, string nextClipId, bool restart = true)
    {
        if (!string.Equals(anim.ClipId, clipId, StringComparison.OrdinalIgnoreCase))
            anim.ClipId = clipId;

        anim.Playing = true;

        if (restart)
            anim.ResetRequested = true;

        anim.NextClipId = nextClipId;
    }
}
