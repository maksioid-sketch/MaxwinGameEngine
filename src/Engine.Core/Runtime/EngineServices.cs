using Engine.Core.Assets;
using Engine.Core.Platform.Input;
using Engine.Core.Platform.Time;

namespace Engine.Core.Runtime;

public sealed class EngineServices
{
    public IInput Input { get; }
    public ITime Time { get; }

    public IAssetProvider Assets { get; set; }

    public EngineServices(IInput input, ITime time, IAssetProvider assets)
    {
        Input = input;
        Time = time;
        Assets = assets;
    }
}
