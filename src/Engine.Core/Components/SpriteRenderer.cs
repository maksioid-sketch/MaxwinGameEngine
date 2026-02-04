using System.Text.Json.Serialization;
using Engine.Core.Math;
using Engine.Core.Rendering;

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

    public SpriteFlip Flip { get; set; } = SpriteFlip.None;

    // ----------------------------
    // NEW: crossfade support (runtime-only)
    // ----------------------------
    [JsonIgnore] public string? PreviousSpriteId { get; set; } = null;
    [JsonIgnore] public float CrossFadeDurationSeconds { get; set; } = 0f;
    [JsonIgnore] public float CrossFadeElapsedSeconds { get; set; } = 0f;

    public void StartCrossFade(string previousSpriteId, float durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(previousSpriteId)) return;
        if (durationSeconds <= 0f) return;

        PreviousSpriteId = previousSpriteId;
        CrossFadeDurationSeconds = durationSeconds;
        CrossFadeElapsedSeconds = 0f;
    }

    public void UpdateCrossFade(float dtSeconds)
    {
        if (PreviousSpriteId is null) return;
        if (CrossFadeDurationSeconds <= 0f) { ClearCrossFade(); return; }

        CrossFadeElapsedSeconds += dtSeconds;
        if (CrossFadeElapsedSeconds >= CrossFadeDurationSeconds)
            ClearCrossFade();
    }

    public void ClearCrossFade()
    {
        PreviousSpriteId = null;
        CrossFadeDurationSeconds = 0f;
        CrossFadeElapsedSeconds = 0f;
    }
}
