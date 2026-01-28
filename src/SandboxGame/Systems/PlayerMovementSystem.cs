using Engine.Core.Scene;
using Engine.Core.Systems;
using Microsoft.Xna.Framework.Input;
using System.Numerics;

namespace SandboxGame.Systems;

public sealed class PlayerMovementSystem : ISystem
{
    public string PlayerEntityName { get; set; } = "Player";
    public float SpeedUnitsPerSecond { get; set; } = 3f;

    public void Update(Scene scene, float dtSeconds)
    {
        var player = scene.FindByName(PlayerEntityName);
        if (player is null) return;

        var k = Keyboard.GetState();

        var move = Vector2.Zero;
        if (k.IsKeyDown(Keys.A)) move.X -= 1f;
        if (k.IsKeyDown(Keys.D)) move.X += 1f;
        if (k.IsKeyDown(Keys.W)) move.Y -= 1f;
        if (k.IsKeyDown(Keys.S)) move.Y += 1f;

        if (move == Vector2.Zero) return;

        move = Vector2.Normalize(move);

        var p = player.Transform.Position;
        p.X += move.X * SpeedUnitsPerSecond * dtSeconds;
        p.Y += move.Y * SpeedUnitsPerSecond * dtSeconds;
        player.Transform.Position = p;
    }
}
