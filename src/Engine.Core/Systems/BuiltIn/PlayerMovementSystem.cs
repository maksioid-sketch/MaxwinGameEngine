using System.Numerics;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;
using Engine.Core.Platform.Input;

namespace Engine.Core.Systems.BuiltIn;

public sealed class PlayerMovementSystem : ISystem
{
    public string PlayerEntityName { get; set; } = "Player";
    public float SpeedUnitsPerSecond { get; set; } = 3f;

    public void Update(Scene.Scene scene, EngineContext ctx)
    {
        var player = scene.FindByName(PlayerEntityName);
        if (player is null) return;


        if (ctx.Input.IsDown(InputKey.Enter))
        {
            const float step = MathF.PI * 0.02f; // 90 degrees
            //RotateAroundZ(player, step);
        }



        var move = Vector2.Zero;

        if (ctx.Input.IsDown(InputKey.A) || ctx.Input.IsDown(InputKey.Left)) move.X -= 1f;
        if (ctx.Input.IsDown(InputKey.D) || ctx.Input.IsDown(InputKey.Right)) move.X += 1f;
        if (ctx.Input.IsDown(InputKey.W) || ctx.Input.IsDown(InputKey.Up)) move.Y -= 1f;
        if (ctx.Input.IsDown(InputKey.S) || ctx.Input.IsDown(InputKey.Down)) move.Y += 1f;

        if (move == Vector2.Zero) return;
        move = Vector2.Normalize(move);

        if (player.TryGet<Engine.Core.Components.SpriteRenderer>(out var sr) && sr != null)
        {
            // If moving left, face left; if moving right, face right
            bool left = ctx.Input.IsDown(InputKey.A) || ctx.Input.IsDown(InputKey.Left);
            bool right = ctx.Input.IsDown(InputKey.D) || ctx.Input.IsDown(InputKey.Right);

            if (left && !right)
                sr.Flip = Engine.Core.Rendering.SpriteFlip.X;
            else if (right && !left)
                sr.Flip = Engine.Core.Rendering.SpriteFlip.None;
        }



        var p = player.Transform.Position;
        p.X += move.X * SpeedUnitsPerSecond * ctx.DeltaSeconds;
        p.Y += move.Y * SpeedUnitsPerSecond * ctx.DeltaSeconds;
        player.Transform.Position = p;
        
    }

    private static void RotateAroundZ(Entity e, float deltaRadians)
    {
        // Create a quaternion representing a Z-axis rotation
        var delta = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, deltaRadians);

        // Apply it. Order matters:
        // delta * current = rotate in world space
        // current * delta = rotate in local space
        e.Transform.Rotation = Quaternion.Normalize(delta * e.Transform.Rotation);
    }


}

