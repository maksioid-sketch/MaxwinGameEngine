using System.Numerics;
using Engine.Core.Components;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;

namespace Engine.Core.Systems.BuiltIn;

public sealed class PhysicsSystem : ISystem
{
    public Vector2 Gravity { get; set; } = new(0f, 9.81f);

    public void Update(Scene.Scene scene, EngineContext ctx)
    {
        var entities = scene.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var e = entities[i];
            if (!e.TryGet<Rigidbody2D>(out var body) || body is null)
                continue;

            if (e.TryGet<PhysicsBody2D>(out var phys) && phys is not null && phys.IsStatic)
                continue;

            var v = body.Velocity;

            if (body.UseGravity)
                v += Gravity * body.GravityScale * ctx.DeltaSeconds;

            if (body.LinearDrag > 0f)
                v *= 1f / (1f + body.LinearDrag * ctx.DeltaSeconds);

            var p = e.Transform.Position;
            p.X += v.X * ctx.DeltaSeconds;
            p.Y += v.Y * ctx.DeltaSeconds;
            e.Transform.Position = p;

            body.Velocity = v;
        }
    }
}
