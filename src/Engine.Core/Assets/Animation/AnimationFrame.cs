namespace Engine.Core.Assets.Animation;

public readonly struct AnimationFrame
{
    public readonly string SpriteId;
    public readonly float DurationSeconds;

    // Optional: Animation notifiers for this frame (fire when entering the frame)
    public readonly string[] Events;

    public AnimationFrame(string spriteId, float durationSeconds, string[]? events = null)
    {
        SpriteId = spriteId;
        DurationSeconds = durationSeconds;
        Events = events ?? System.Array.Empty<string>();
    }
}
