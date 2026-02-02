using Engine.Core.Assets;
using Engine.Core.Platform.Input;
using Engine.Core.Platform.Time;
using Engine.Core.Runtime.Events;

namespace Engine.Core.Runtime;

public sealed class EngineServices
{
    public IInput Input { get; }
    public ITime Time { get; }

    public IAssetProvider Assets { get; set; }

    public IEventBus Events { get; }

    public EngineServices(IInput input, ITime time, IAssetProvider assets, IEventBus events)
    {
        Input = input;
        Time = time;
        Assets = assets;
        Events = events;
    }
}
