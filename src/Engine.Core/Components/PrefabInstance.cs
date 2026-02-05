namespace Engine.Core.Components;

public sealed class PrefabInstance : IComponent
{
    public string PrefabId { get; set; } = string.Empty;

    // If true, apply the prefab's root transform when resolving the instance.
    public bool UsePrefabTransform { get; set; } = true;

    // Override flags are set from scene JSON presence.
    public bool OverrideTransform { get; set; } = false;
    public bool OverrideSpriteRenderer { get; set; } = false;
    public bool OverrideAnimator { get; set; } = false;
    public bool OverrideBoxCollider2D { get; set; } = false;
    public bool OverridePhysicsBody2D { get; set; } = false;
    public bool OverrideRigidbody2D { get; set; } = false;
    public bool OverrideDebugRender2D { get; set; } = false;
}
