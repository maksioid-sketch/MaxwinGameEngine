using Engine.Core.Math;

namespace Engine.Core.Components;

public sealed class SpriteRenderer : IComponent
{
    // Logical sprite id (from atlas/asset provider), e.g. "player"
    public string SpriteId { get; set; } = string.Empty;

    // Optional override tint/layer
    public Color4 Tint { get; set; } = Color4.White;
    public int Layer { get; set; } = 0;

    // Optional overrides (leave default to use atlas values)
    public bool OverrideSourceRect { get; set; } = false;
    public IntRect SourceRectOverride { get; set; } = new(0, 0, 0, 0);

    public bool OverridePixelsPerUnit { get; set; } = false;
    public float PixelsPerUnitOverride { get; set; } = 100f;
}
