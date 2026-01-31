using System.Text.Json.Serialization;

namespace Engine.Core.Components;

public sealed class Animator : IComponent
{
    // Which clip to play (from animations.json)
    public string ClipId { get; set; } = "";

    public bool Playing { get; set; } = true;
    public bool LoopOverride { get; set; } = false; // if true, use Loop instead of clip.Loop
    public bool Loop { get; set; } = true;

    public float Speed { get; set; } = 1f; // 1.0 = normal speed

    // Runtime state (serialized is fine, but you can omit later if you want)
    public int FrameIndex { get; set; } = 0;
    public float TimeIntoFrame { get; set; } = 0f;


    [JsonIgnore]
    public string? NextClipId { get; set; } = null;

    [JsonIgnore]
    public bool ResetRequested { get; set; } = false;

}
