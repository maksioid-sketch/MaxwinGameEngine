using Engine.Core.Platform.Input;
using Engine.Core.Runtime;
using Engine.Core.Scene;
using Engine.Core.Systems;
using SandboxGame.Events;

namespace SandboxGame.Systems;

public sealed class DebugDamageInputSystem : ISystem
{
    public string TargetEntityName { get; set; } = "Player";
    public InputKey Key { get; set; } = InputKey.E;
    public int Amount { get; set; } = 1;

    public void Update(Scene scene, EngineContext ctx)
    {
        if (!ctx.Input.WasPressed(Key))
            return;

        var target = scene.FindByName(TargetEntityName);
        if (target is null)
            return;

        ctx.Events.Publish(new DamageEvent(target.Id, Amount));
    }
}
