using Engine.Core.Runtime;
using Engine.Core.Scene;

namespace Engine.Core.Systems;

public interface ISystem
{
    void Update(Scene.Scene scene, EngineContext ctx);
}
