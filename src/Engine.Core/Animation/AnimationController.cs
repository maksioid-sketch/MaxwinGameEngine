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
    public float Speed { get; set; } = 1f;
}

public sealed class ControllerTransition
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    public string? TransitionClipId { get; set; } = null;
    public float TransitionSpeed { get; set; } = 1f;

    public int Priority { get; set; } = 0;
    public bool CanInterrupt { get; set; } = false;

    // Robustness
    public float ExitTime { get; set; } = -1f;
    public float MinTimeInState { get; set; } = 0f;

    // NEW: if >= 0, overrides Animator.CrossFadeSeconds for this switch
    public float CrossFadeSeconds { get; set; } = -1f;

    // Optional: if set in json, overrides freeze behavior for this transition only
    public bool? FreezeDuringCrossFade { get; set; } = null;


    public List<TransitionCondition>? Conditions { get; set; } = null;

    // Back-compat input-only
    public TransitionWhen When { get; set; } = new();
}

public enum CompareOp
{
    Eq, Ne, Gt, Ge, Lt, Le
}

public sealed class TransitionCondition
{
    public string? Trigger { get; set; } = null;

    // Finished signal
    public bool? Finished { get; set; } = null;

    public string? Bool { get; set; } = null;
    public bool BoolValue { get; set; } = true;

    public string? Float { get; set; } = null;
    public CompareOp Op { get; set; } = CompareOp.Gt;
    public float Value { get; set; } = 0f;

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
