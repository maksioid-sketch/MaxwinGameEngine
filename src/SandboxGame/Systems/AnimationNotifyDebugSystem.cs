using Engine.Core.Runtime;
using Engine.Core.Runtime.Debug;
using Engine.Core.Runtime.Events;
using Engine.Core.Scene;
using Engine.Core.Systems;

namespace SandboxGame.Systems;

public sealed class AnimationNotifyDebugSystem : ISystem
{
    public float DefaultSeconds { get; set; } = 1.2f;

    public void Update(Scene scene, EngineContext ctx)
    {
        var events = ctx.Events.Read<AnimationNotifyEvent>();
        if (events.Count == 0) return;

        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            DebugPrint.Print($"[Notify] {ev.EntityName} :: {ev.Name} (clip={ev.ClipId} frame={ev.FrameIndex})", DefaultSeconds);
            DebugPrint.Print("hello");
        }
    }
}
