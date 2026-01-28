using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Engine.Core.Rendering;

public sealed class Camera2D
{
    public Vector2 Position { get; set; } = Vector2.Zero;
    public float RotationRadians { get; set; } = 0f;
    public float Zoom { get; set; } = 1f;

    public int ViewportWidth { get; set; } = 1280;
    public int ViewportHeight { get; set; } = 720;

    public Matrix4x4 GetViewMatrix()
    {
        // World -> View (camera transform inverse)
        var translate = Matrix4x4.CreateTranslation(-Position.X, -Position.Y, 0f);
        var rotate = Matrix4x4.CreateRotationZ(-RotationRadians);
        var scale = Matrix4x4.CreateScale(Zoom, Zoom, 1f);
        return translate * rotate * scale;
    }

    public Matrix4x4 GetProjectionMatrix()
    {
        // Screen space: (0,0) top-left, (W,H) bottom-right
        return Matrix4x4.CreateOrthographicOffCenter(
            0f, ViewportWidth,
            ViewportHeight, 0f,
            0f, 1f);
    }
}

