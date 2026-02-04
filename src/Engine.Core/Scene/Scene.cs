using Engine.Core.Assets;
using Engine.Core.Assets;
using Engine.Core.Components;
using Engine.Core.Rendering;
using Engine.Core.Rendering.Queue;
using System.Numerics;


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

    public Entity InstantiatePrefab(Prefab prefab, Vector3? positionOverride = null)
    {
        if (prefab is null) throw new ArgumentNullException(nameof(prefab));
        return prefab.Instantiate(this, positionOverride);
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

            System.Numerics.Vector2 ComputeOrigin(Engine.Core.Assets.SpriteDefinition s, Engine.Core.Math.IntRect src)
            {
                var origin = s.OriginPixels;

                if (origin == System.Numerics.Vector2.Zero && s.DefaultOriginToCenter)
                {
                    if (src.W > 0 && src.H > 0)
                        origin = new System.Numerics.Vector2(src.W * 0.5f, src.H * 0.5f);
                }

                return origin;
            }

            // Base tint premultiplied by its OWN alpha (important for BlendState.AlphaBlend)
            var baseTintPremul = new Engine.Core.Math.Color4(
                sr.Tint.R * sr.Tint.A,
                sr.Tint.G * sr.Tint.A,
                sr.Tint.B * sr.Tint.A,
                sr.Tint.A
            );

            if (sr.PreviousSpriteId is not null &&
                sr.CrossFadeDurationSeconds > 0f &&
                sr.CrossFadeElapsedSeconds < sr.CrossFadeDurationSeconds &&
                assets.TryGetSprite(sr.PreviousSpriteId, out var prevSprite))
            {
                float t = sr.CrossFadeElapsedSeconds / sr.CrossFadeDurationSeconds;
                if (t < 0f) t = 0f;
                if (t > 1f) t = 1f;

                float oldK = 1f - t;
                float newK = t;

                // Apply weights in premultiplied space
                var oldTint = new Engine.Core.Math.Color4(
                    baseTintPremul.R * oldK,
                    baseTintPremul.G * oldK,
                    baseTintPremul.B * oldK,
                    baseTintPremul.A * oldK
                );

                var newTint = new Engine.Core.Math.Color4(
                    baseTintPremul.R * newK,
                    baseTintPremul.G * newK,
                    baseTintPremul.B * newK,
                    baseTintPremul.A * newK
                );

                var prevSrc = sr.OverrideSourceRect ? sr.SourceRectOverride : prevSprite.SourceRect;
                var prevPpu = sr.OverridePixelsPerUnit ? sr.PixelsPerUnitOverride : prevSprite.PixelsPerUnit;
                var prevOrigin = ComputeOrigin(prevSprite, prevSrc);

                output.Add(new RenderItem2D(
                    textureKey: prevSprite.TextureKey,
                    worldPosition: new System.Numerics.Vector3(pos.X, pos.Y, pos.Z - 0.0001f),
                    worldScale: scale2,
                    rotationRadians: rotZ,
                    sourceRect: prevSrc,
                    tint: oldTint,
                    layer: sr.Layer,
                    pixelsPerUnit: prevPpu,
                    originPixels: prevOrigin,
                    flip: sr.Flip
                ));

                var srcNow = sr.OverrideSourceRect ? sr.SourceRectOverride : sprite.SourceRect;
                var ppuNow = sr.OverridePixelsPerUnit ? sr.PixelsPerUnitOverride : sprite.PixelsPerUnit;
                var originNow = ComputeOrigin(sprite, srcNow);

                output.Add(new RenderItem2D(
                    textureKey: sprite.TextureKey,
                    worldPosition: pos,
                    worldScale: scale2,
                    rotationRadians: rotZ,
                    sourceRect: srcNow,
                    tint: newTint,
                    layer: sr.Layer,
                    pixelsPerUnit: ppuNow,
                    originPixels: originNow,
                    flip: sr.Flip
                ));

                continue;
            }

            // Normal draw: use premultiplied tint as well (fixes subtle brightening whenever A != 1)
            var src = sr.OverrideSourceRect ? sr.SourceRectOverride : sprite.SourceRect;
            var ppu = sr.OverridePixelsPerUnit ? sr.PixelsPerUnitOverride : sprite.PixelsPerUnit;
            var origin2 = ComputeOrigin(sprite, src);

            output.Add(new RenderItem2D(
                textureKey: sprite.TextureKey,
                worldPosition: pos,
                worldScale: scale2,
                rotationRadians: rotZ,
                sourceRect: src,
                tint: baseTintPremul,
                layer: sr.Layer,
                pixelsPerUnit: ppu,
                originPixels: origin2,
                flip: sr.Flip
            ));
        }
    }

}
