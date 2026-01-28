using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Engine.Core.Components;
using Engine.Core.Rendering;

namespace Engine.Core.Scene;

public sealed class Scene
{
    private readonly List<Entity> _entities = new();

    public IReadOnlyList<Entity> Entities => _entities;

    public Entity CreateEntity(string name)
    {
        var e = new Entity { Name = name };
        _entities.Add(e);
        return e;
    }

    public void Update(float dtSeconds)
    {
        // Later: systems, scripts, physics, etc.
        // For now, the scene is mostly data.
    }

    public void Render(IRenderer2D renderer2D)
    {
        // Minimal render traversal:
        // later: build a render queue and sort by layer/material/etc.
        foreach (var e in _entities)
        {
            if (!e.TryGet<SpriteRenderer>(out var spr) || spr is null) continue;

            // Use Transform’s 3D position; 2D is the XY plane (Z optional).
            var pos = e.Transform.Position;

            // Convert 3D scale to 2D scale for sprites:
            var scale2 = new System.Numerics.Vector2(e.Transform.Scale.X, e.Transform.Scale.Y);

            // 2D rotation is around Z:
            var rotZ = GetZRotationRadians(e.Transform.Rotation);

            renderer2D.DrawSprite(
                spr.TextureKey,
                pos,
                scale2,
                rotZ,
                spr.SourceRect,
                spr.Tint,
                spr.Layer,
                spr.PixelsPerUnit);
        }
    }

    private static float GetZRotationRadians(System.Numerics.Quaternion q)
    {
        // Extract yaw-like rotation around Z for a pure 2D case.
        // If you keep 2D rotations around Z only, this stays stable.
        var siny_cosp = 2f * (q.W * q.Z + q.X * q.Y);
        var cosy_cosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        return (float)System.Math.Atan2(siny_cosp, cosy_cosp);
    }
}

