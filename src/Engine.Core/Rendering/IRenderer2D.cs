using System.Numerics;
using Engine.Core.Math;

namespace Engine.Core.Rendering;

public interface IRenderer2D
{
    void Begin(Camera2D camera);
    void End();

    void DrawSprite(
        string textureKey,
        Vector3 worldPos,
        Vector2 worldScale,
        float rotationRadians,
        IntRect sourceRect,
        Color4 tint,
        int layer,
        float spritePixelsPerUnit,
        Vector2 originPixels,
        Engine.Core.Rendering.SpriteFlip flip
        );
}
