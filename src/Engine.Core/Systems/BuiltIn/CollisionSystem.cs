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
            var offsetLocal = new Vector2(col.Offset.X * scale.X, col.Offset.Y * scale.Y);
            var center = new Vector2(e.Transform.Position.X, e.Transform.Position.Y);
            var rot = GetZRotationRadians(e.Transform.Rotation);

            var axisX = new Vector2(System.MathF.Cos(rot), System.MathF.Sin(rot));
            var axisY = new Vector2(-axisX.Y, axisX.X);
            var offsetWorld = axisX * offsetLocal.X + axisY * offsetLocal.Y;
            center += offsetWorld;

            _entries.Add(new ColliderEntry(e, col, center, size * 0.5f, rot, axisX, axisY));
        }

        for (int i = 0; i < _entries.Count; i++)
        {
            for (int j = i + 1; j < _entries.Count; j++)
            {
                var a = _entries[i];
                var b = _entries[j];

                if (Intersects(a, b))
                {
                    ctx.Events.Publish(new CollisionEvent(a.Entity, b.Entity));

                    if (!a.Collider.IsTrigger && !b.Collider.IsTrigger)
                    {
                        ResolveOverlap(ref a, ref b);
                        _entries[i] = a;
                        _entries[j] = b;
                    }
                }
            }
        }
    }

    private static bool Intersects(in ColliderEntry a, in ColliderEntry b)
    {
        return GetMinSeparationAxis(a, b, out _, out _) > 0f;
    }

    private static void ResolveOverlap(ref ColliderEntry a, ref ColliderEntry b)
    {
        var minOverlap = GetMinSeparationAxis(a, b, out var axis, out var axisSign);
        if (minOverlap <= 0f)
            return;

        var move = axis * (minOverlap * 0.5f * axisSign);

        var aPos = a.Entity.Transform.Position;
        aPos.X -= move.X;
        aPos.Y -= move.Y;
        a.Entity.Transform.Position = aPos;
        a.Center -= move;

        var bPos = b.Entity.Transform.Position;
        bPos.X += move.X;
        bPos.Y += move.Y;
        b.Entity.Transform.Position = bPos;
        b.Center += move;
    }

    private static float GetMinSeparationAxis(in ColliderEntry a, in ColliderEntry b, out Vector2 axisOut, out float axisSign)
    {
        axisOut = Vector2.UnitX;
        axisSign = 1f;
        float minOverlap = float.MaxValue;

        Span<Vector2> axes = stackalloc Vector2[4];
        axes[0] = a.AxisX;
        axes[1] = a.AxisY;
        axes[2] = b.AxisX;
        axes[3] = b.AxisY;

        var delta = b.Center - a.Center;

        for (int i = 0; i < axes.Length; i++)
        {
            var axis = axes[i];
            var overlap = GetOverlapOnAxis(a, b, axis);
            if (overlap <= 0f)
                return 0f;

            if (overlap < minOverlap)
            {
                minOverlap = overlap;
                axisOut = axis;
                axisSign = Vector2.Dot(delta, axis) >= 0f ? 1f : -1f;
            }
        }

        return minOverlap;
    }

    private static float GetOverlapOnAxis(in ColliderEntry a, in ColliderEntry b, Vector2 axis)
    {
        Project(a, axis, out var minA, out var maxA);
        Project(b, axis, out var minB, out var maxB);
        return System.MathF.Min(maxA, maxB) - System.MathF.Max(minA, minB);
    }

    private static void Project(in ColliderEntry e, Vector2 axis, out float min, out float max)
    {
        var ax = e.AxisX;
        var ay = e.AxisY;
        var hx = e.HalfSize.X;
        var hy = e.HalfSize.Y;

        var p0 = e.Center + ax * hx + ay * hy;
        var p1 = e.Center + ax * hx - ay * hy;
        var p2 = e.Center - ax * hx + ay * hy;
        var p3 = e.Center - ax * hx - ay * hy;

        min = max = Vector2.Dot(p0, axis);
        var v = Vector2.Dot(p1, axis); if (v < min) min = v; if (v > max) max = v;
        v = Vector2.Dot(p2, axis); if (v < min) min = v; if (v > max) max = v;
        v = Vector2.Dot(p3, axis); if (v < min) min = v; if (v > max) max = v;
    }

    private static float GetZRotationRadians(System.Numerics.Quaternion q)
    {
        var siny_cosp = 2f * (q.W * q.Z + q.X * q.Y);
        var cosy_cosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
        return (float)System.Math.Atan2(siny_cosp, cosy_cosp);
    }

    private struct ColliderEntry
    {
        public Entity Entity;
        public BoxCollider2D Collider;
        public Vector2 Center;
        public Vector2 HalfSize;
        public float Rotation;
        public Vector2 AxisX;
        public Vector2 AxisY;

        public ColliderEntry(Entity entity, BoxCollider2D collider, Vector2 center, Vector2 halfSize, float rotation, Vector2 axisX, Vector2 axisY)
        {
            Entity = entity;
            Collider = collider;
            Center = center;
            HalfSize = halfSize;
            Rotation = rotation;
            AxisX = axisX;
            AxisY = axisY;
        }
    }
}
