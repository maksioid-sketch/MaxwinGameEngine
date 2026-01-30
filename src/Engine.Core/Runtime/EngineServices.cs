using Engine.Core.Assets;
using Engine.Core.Platform.Input;
using Engine.Core.Platform.Time;

namespace Engine.Core.Runtime;

public sealed class EngineServices
{
    public IInput Input { get; }
    public ITime Time { get; }

    // Assets can change on hot reload, so we allow replacing it safely.
    public IAssetProvider Assets { get; private set; }

    public EngineServices(IInput input, ITime time, IAssetProvider assets)
    {
        Input = input;
        Time = time;
        Assets = assets;
    }

    public void SetAssets(IAssetProvider assets)
    {
        Assets = assets;
    }
}
