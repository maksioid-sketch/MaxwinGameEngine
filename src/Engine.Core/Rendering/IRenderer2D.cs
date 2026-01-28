using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using Engine.Core.Math;

namespace Engine.Core.Rendering;

public interface IRenderer2D
{
    void Begin(Camera2D camera);
    void DrawSprite(
        string textureKey,
        Vector3 worldPosition,
        Vector2 worldScale,
        float rotationRadians,
        IntRect sourceRect,
        Color4 tint,
        int layer,
        float pixelsPerUnit);
    void End();
}

