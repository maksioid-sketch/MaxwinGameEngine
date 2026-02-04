using Engine.Core.Components;
using Engine.Core.Runtime;
using Engine.Core.Runtime.Debug;
using Engine.Core.Scene;
using Engine.Core.Systems;
using SandboxGame.Events;
using System;

namespace SandboxGame.Systems;

public sealed class DamageToAnimatorTriggerSystem : ISystem
{
    public string TriggerName { get; set; } = "damaged";

    public void Update(Scene scene, EngineContext ctx)
    {
        var events = ctx.Events.Read<DamageEvent>();
        if (events.Count == 0) return;

        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];

            Entity target = null;
            foreach (var e in scene.Entities)
            {
                if (e.Id == ev.EntityId) { target = e; break; }
            }

            if (target is null) continue;

            if (target.TryGet<Animator>(out var anim) && anim is not null)
                anim.SetTrigger(TriggerName);

            DebugPrint.Print("Damaged!", 1.5f);

        }
    }
}
