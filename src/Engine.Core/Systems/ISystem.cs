namespace Engine.Core.Systems;

public interface ISystem
{
    void Update(Engine.Core.Scene.Scene scene, float dtSeconds);
}
