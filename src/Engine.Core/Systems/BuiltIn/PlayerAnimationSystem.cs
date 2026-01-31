using Engine.Core.Components;
using Engine.Core.Platform.Input;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;

namespace Engine.Core.Systems.BuiltIn;

public sealed class PlayerAnimationStateSystem : ISystem
{
    public string PlayerEntityName { get; set; } = "Player";

    public string IdleClipId { get; set; } = "player_idle";
    public string WalkClipId { get; set; } = "player_walk";

    public void Update(Scene.Scene scene, EngineContext ctx)
    {
        var player = scene.FindByName(PlayerEntityName);
        if (player is null) return;

        if (!player.TryGet<Animator>(out var anim) || anim is null) return;

        bool moving =
            ctx.Input.IsDown(InputKey.W) || ctx.Input.IsDown(InputKey.A) ||
            ctx.Input.IsDown(InputKey.S) || ctx.Input.IsDown(InputKey.D) ||
            ctx.Input.IsDown(InputKey.Up) || ctx.Input.IsDown(InputKey.Left) ||
            ctx.Input.IsDown(InputKey.Down) || ctx.Input.IsDown(InputKey.Right);

        var desired = moving ? WalkClipId : IdleClipId;

        if (!string.Equals(anim.ClipId, desired, StringComparison.OrdinalIgnoreCase))
        {
            anim.ClipId = desired;
            anim.Playing = true;
            anim.ResetRequested = true;
        }
    }
}
