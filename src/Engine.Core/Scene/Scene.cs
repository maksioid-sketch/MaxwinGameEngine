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

    public Entity CreateEntity(Guid id, string name)
    {
        var e = new Entity(id) { Name = name };
        _entities.Add(e);
        return e;
    }

    public Entity? FindByName(string name) =>
        _entities.FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    public void Update(float dtSeconds)
    {
        // Keep this as “data only”. Put gameplay logic into Systems (next step).
    }

    public void Render(IRenderer2D renderer2D)
    {
        foreach (var e in _entities)
        {
            if (!e.TryGet<SpriteRenderer>(out var spr) || spr is null) continue;

            var pos = e.Transform.Position;
            var scale2 = new System.Numerics.Vector2(e.Transform.Scale.X, e.Transform.Scale.Y);
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
        var siny_cosp = 2f * (q.W * q.Z + q.X * q.Y);
        var cosy_cosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        return (float)System.Math.Atan2(siny_cosp, cosy_cosp);
    }
}
