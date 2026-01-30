using System.Numerics;

namespace Engine.Core.Rendering;

public sealed class Camera2D
{
    // World-space camera position (units)
    public Vector2 Position { get; set; } = Vector2.Zero;

    // Rotation in radians (positive = CCW)
    public float Rotation { get; set; } = 0f;

    // 1 = default; >1 zooms in; <1 zooms out
    public float Zoom { get; set; } = 1f;

    // Global world→pixel scale. Example: 100 means 1 world unit = 100 pixels (at zoom=1).
    public float PixelsPerUnit { get; set; } = 100f;

    // For pixel-art: snap final screen position to integer pixels
    public bool PixelSnap { get; set; } = true;

    // Set each frame from GraphicsDevice viewport
    public int ViewportWidth { get; set; }
    public int ViewportHeight { get; set; }

    public Vector2 ViewportCenter => new(ViewportWidth * 0.5f, ViewportHeight * 0.5f);

    public Vector2 WorldToScreen(Vector2 world)
    {
        var rel = world - Position;
        rel = Rotate(rel, -Rotation);

        var px = rel * (PixelsPerUnit * Zoom) + ViewportCenter;

        if (PixelSnap)
            px = new Vector2(MathF.Round(px.X), MathF.Round(px.Y));

        return px;
    }

    public Vector2 ScreenToWorld(Vector2 screen)
    {
        var rel = (screen - ViewportCenter) / (PixelsPerUnit * Zoom);
        rel = Rotate(rel, Rotation);
        return rel + Position;
    }

    private static Vector2 Rotate(Vector2 v, float radians)
    {
        float c = MathF.Cos(radians);
        float s = MathF.Sin(radians);
        return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
    }
}
