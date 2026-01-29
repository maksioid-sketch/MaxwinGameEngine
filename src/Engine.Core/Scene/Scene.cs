using Engine.Core.Components;
using Engine.Core.Rendering;
using Engine.Core.Assets;
using Engine.Core.Assets;
using Engine.Core.Rendering.Queue;


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

    public void Render(IRenderer2D renderer2D, IAssetProvider assets)
    {
        foreach (var e in _entities)
        {
            if (!e.TryGet<Components.SpriteRenderer>(out var sr) || sr is null)
                continue;

            if (string.IsNullOrWhiteSpace(sr.SpriteId))
                continue;

            if (!assets.TryGetSprite(sr.SpriteId, out var sprite))
                continue;

            var pos = e.Transform.Position;
            var scale2 = new System.Numerics.Vector2(e.Transform.Scale.X, e.Transform.Scale.Y);
            var rotZ = GetZRotationRadians(e.Transform.Rotation);

            var src = sr.OverrideSourceRect ? sr.SourceRectOverride : sprite.SourceRect;
            var ppu = sr.OverridePixelsPerUnit ? sr.PixelsPerUnitOverride : sprite.PixelsPerUnit;

            renderer2D.DrawSprite(
                sprite.TextureKey,
                pos,
                scale2,
                rotZ,
                src,
                sr.Tint,
                sr.Layer,
                ppu);
        }
    }

    private static float GetZRotationRadians(System.Numerics.Quaternion q)
    {
        var siny_cosp = 2f * (q.W * q.Z + q.X * q.Y);
        var cosy_cosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        return (float)System.Math.Atan2(siny_cosp, cosy_cosp);
    }

    public void CollectRenderItems2D(List<RenderItem2D> output, IAssetProvider assets)
    {
        output.Clear();

        foreach (var e in _entities)
        {
            if (!e.TryGet<Engine.Core.Components.SpriteRenderer>(out var sr) || sr is null)
                continue;

            if (string.IsNullOrWhiteSpace(sr.SpriteId))
                continue;

            if (!assets.TryGetSprite(sr.SpriteId, out var sprite))
                continue;

            var pos = e.Transform.Position;
            var scale2 = new System.Numerics.Vector2(e.Transform.Scale.X, e.Transform.Scale.Y);
            var rotZ = GetZRotationRadians(e.Transform.Rotation);

            var src = sr.OverrideSourceRect ? sr.SourceRectOverride : sprite.SourceRect;
            var ppu = sr.OverridePixelsPerUnit ? sr.PixelsPerUnitOverride : sprite.PixelsPerUnit;

            output.Add(new RenderItem2D(
                textureKey: sprite.TextureKey,
                worldPosition: pos,
                worldScale: scale2,
                rotationRadians: rotZ,
                sourceRect: src,
                tint: sr.Tint,
                layer: sr.Layer,
                pixelsPerUnit: ppu));
        }
    }

}
