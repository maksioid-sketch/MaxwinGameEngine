namespace Engine.Core.Assets.Animation;

public sealed class AnimatorController
{
    public string InitialState { get; set; } = "idle";
    public Dictionary<string, ControllerState> States { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ControllerTransition> Transitions { get; set; } = new();
}

public sealed class ControllerState
{
    public string ClipId { get; set; } = "";
}

public sealed class ControllerTransition
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    // Optional: plays this non-looping clip first, then auto-switches to To state's clip.
    public string? TransitionClipId { get; set; } = null;

    public TransitionWhen When { get; set; } = new();
}

public sealed class TransitionWhen
{
    public string[]? PressedAny { get; set; } = null;
    public string[]? DownAny { get; set; } = null;
    public string[]? NoneDown { get; set; } = null;
}
