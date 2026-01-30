using System.Numerics;
using Engine.Core.Math;

namespace Engine.Core.Assets;

public sealed class SpriteDefinition
{
    // MGCB texture key (e.g. "Sprites/playerAtlas")
    public string TextureKey { get; set; } = string.Empty;

    // Source rectangle inside the texture (pixels). [0,0,0,0] means full texture.
    public IntRect SourceRect { get; set; } = new(0, 0, 0, 0);

    // Pixels per world unit
    public float PixelsPerUnit { get; set; } = 100f;

    // Pivot/origin in SOURCE pixels (relative to SourceRect)
    // Example: (32, 32) for center of a 64x64 sprite.
    public Vector2 OriginPixels { get; set; } = Vector2.Zero;

    // If true and OriginPixels is zero, treat origin as sprite center by default.
    public bool DefaultOriginToCenter { get; set; } = true;
}
