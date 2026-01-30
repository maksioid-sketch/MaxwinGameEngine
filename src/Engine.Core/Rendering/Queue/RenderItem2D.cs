using System.Numerics;
using Engine.Core.Math;

namespace Engine.Core.Rendering.Queue;

public readonly struct RenderItem2D
{
    public readonly string TextureKey;
    public readonly Vector3 WorldPosition;
    public readonly Vector2 WorldScale;
    public readonly float RotationRadians;
    public readonly IntRect SourceRect;
    public readonly Color4 Tint;
    public readonly int Layer;
    public readonly float PixelsPerUnit;

    public readonly Vector2 OriginPixels; // NEW

    public RenderItem2D(
        string textureKey,
        Vector3 worldPosition,
        Vector2 worldScale,
        float rotationRadians,
        IntRect sourceRect,
        Color4 tint,
        int layer,
        float pixelsPerUnit,
        Vector2 originPixels)
    {
        TextureKey = textureKey;
        WorldPosition = worldPosition;
        WorldScale = worldScale;
        RotationRadians = rotationRadians;
        SourceRect = sourceRect;
        Tint = tint;
        Layer = layer;
        PixelsPerUnit = pixelsPerUnit;
        OriginPixels = originPixels;
    }
}
