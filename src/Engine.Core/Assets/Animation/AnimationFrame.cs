namespace Engine.Core.Assets.Animation;

public readonly struct AnimationFrame
{
    public readonly string SpriteId;
    public readonly float DurationSeconds;

    public AnimationFrame(string spriteId, float durationSeconds)
    {
        SpriteId = spriteId;
        DurationSeconds = durationSeconds;
    }
}
