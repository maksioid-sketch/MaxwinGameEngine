namespace Engine.Core.Assets.Animation;

public sealed class AnimationClip
{
    public string Id { get; set; } = "";
    public bool Loop { get; set; } = true;

    public List<AnimationFrame> Frames { get; set; } = new();

    public float TotalLengthSeconds
    {
        get
        {
            float total = 0f;
            for (int i = 0; i < Frames.Count; i++)
                total += System.Math.Max(0.0001f, Frames[i].DurationSeconds);
            return total;
        }
    }
}
