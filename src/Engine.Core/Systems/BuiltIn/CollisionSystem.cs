using System;
using System.Collections.Generic;
using System.Numerics;
using Engine.Core.Components;
using Engine.Core.Runtime;
using Engine.Core.Runtime.Events;
using Engine.Core.Scene;

namespace Engine.Core.Systems.BuiltIn;

public sealed class CollisionSystem : ISystem
{
    private readonly List<ColliderEntry> _entries = new();

    public void Update(Scene.Scene scene, EngineContext ctx)
    {
        _entries.Clear();

        var entities = scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            if (!e.TryGet<BoxCollider2D>(out var col) || col is null)
                continue;

            var scale = e.Transform.Scale;
            var size = new Vector2(System.MathF.Abs(scale.X) * col.Size.X, System.MathF.Abs(scale.Y) * col.Size.Y);
            var offset = new Vector2(col.Offset.X * scale.X, col.Offset.Y * scale.Y);
            var center = new Vector2(e.Transform.Position.X, e.Transform.Position.Y) + offset;

            _entries.Add(new ColliderEntry(e, col, center, size * 0.5f));
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            for (int j = i + 1; j < _entries.Count; j++)
            {
                if (Intersects(_entries[i], _entries[j]))
                    ctx.Events.Publish(new CollisionEvent(_entries[i].Entity, _entries[j].Entity));
            }
        }
    }

    private static bool Intersects(in ColliderEntry a, in ColliderEntry b)
    {
        return System.MathF.Abs(a.Center.X - b.Center.X) <= (a.HalfSize.X + b.HalfSize.X) &&
               System.MathF.Abs(a.Center.Y - b.Center.Y) <= (a.HalfSize.Y + b.HalfSize.Y);
    }

    private readonly struct ColliderEntry
    {
        public readonly Entity Entity;
        public readonly BoxCollider2D Collider;
        public readonly Vector2 Center;
        public readonly Vector2 HalfSize;

        public ColliderEntry(Entity entity, BoxCollider2D collider, Vector2 center, Vector2 halfSize)
        {
            Entity = entity;
            Collider = collider;
            Center = center;
            HalfSize = halfSize;
        }
    }
}
