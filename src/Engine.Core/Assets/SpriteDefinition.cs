using Engine.Core.Math;

namespace Engine.Core.Assets;

public sealed class SpriteDefinition
{
    // The MGCB texture key (e.g. "atlas_01" or "player")
    public string TextureKey { get; set; } = string.Empty;

    // Source rectangle inside the texture (pixels)
    public IntRect SourceRect { get; set; } = new(0, 0, 0, 0);

    // Pixels per world unit
    public float PixelsPerUnit { get; set; } = 100f;
}
