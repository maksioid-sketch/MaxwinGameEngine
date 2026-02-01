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

    // Multiplies Animator.Speed while this state is active (1 = normal)
    public float Speed { get; set; } = 1f;
}

public sealed class ControllerTransition
{
    // Use "*" to mean Any State
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    // Optional: plays this non-looping clip first, then auto-switches to To state's clip.
    public string? TransitionClipId { get; set; } = null;

    // Optional speed while playing TransitionClipId (ignored for direct switches)
    public float TransitionSpeed { get; set; } = 1f;

    // Higher wins (useful when multiple transitions match)
    public int Priority { get; set; } = 0;

    // If true, a transition can fire even while a transition clip is already playing
    public bool CanInterrupt { get; set; } = false;

    // New flexible condition list (AND across all conditions)
    public List<TransitionCondition>? Conditions { get; set; } = null;

    // Legacy (v1) input-only conditions; kept for backward compatibility
    public TransitionWhen When { get; set; } = new();
}

public enum CompareOp
{
    Eq, Ne, Gt, Ge, Lt, Le
}

public sealed class TransitionCondition
{
    // One-shot event name (consumed only when the transition is taken)
    public string? Trigger { get; set; } = null;

    public string? Bool { get; set; } = null;
    public bool BoolValue { get; set; } = true;

    public string? Float { get; set; } = null;
    public CompareOp Op { get; set; } = CompareOp.Gt;
    public float Value { get; set; } = 0f;

    // Optional: legacy input checks usable as a condition
    public string[]? PressedAny { get; set; } = null;
    public string[]? DownAny { get; set; } = null;
    public string[]? NoneDown { get; set; } = null;
}

public sealed class TransitionWhen
{
    public string[]? PressedAny { get; set; } = null;
    public string[]? DownAny { get; set; } = null;
    public string[]? NoneDown { get; set; } = null;
}
