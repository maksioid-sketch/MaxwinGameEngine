using Engine.Core.Assets;
using Engine.Core.Platform.Input;
using Engine.Core.Platform.Time;
using Engine.Core.Runtime.Events;

namespace Engine.Core.Runtime;

public readonly struct EngineContext
{
    public EngineServices Services { get; }

    public EngineContext(EngineServices services)
    {
        Services = services;
    }

    // Convenience accessors
    public IInput Input => Services.Input;
    public ITime Time => Services.Time;
    public IAssetProvider Assets => Services.Assets;
    public IEventBus Events => Services.Events;

    public float DeltaSeconds => Services.Time.DeltaSeconds;
    public double TotalSeconds => Services.Time.TotalSeconds;
}
