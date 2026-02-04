using Engine.Core.Assets.Animation;
using Engine.Core.Scene;

namespace Engine.Core.Assets;

public interface IAssetProvider
{
    bool TryGetSprite(string spriteId, out SpriteDefinition sprite);
    bool TryGetAnimation(string clipId, out AnimationClip clip);

    bool TryGetController(string controllerId, out AnimatorController controller);

    bool TryGetPrefab(string prefabId, out Prefab prefab);
}
