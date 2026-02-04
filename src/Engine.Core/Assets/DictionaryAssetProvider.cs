using Engine.Core.Assets.Animation;
using Engine.Core.Scene;

namespace Engine.Core.Assets;

public sealed class DictionaryAssetProvider : IAssetProvider
{
    private readonly Dictionary<string, SpriteDefinition> _sprites;
    private readonly Dictionary<string, AnimationClip> _clips;
    private readonly Dictionary<string, AnimatorController> _controllers;
    private readonly Dictionary<string, Prefab> _prefabs;

    public DictionaryAssetProvider(
        Dictionary<string, SpriteDefinition> sprites,
        Dictionary<string, AnimationClip> clips,
        Dictionary<string, AnimatorController> controllers,
        Dictionary<string, Prefab> prefabs)
    {
        _sprites = sprites;
        _clips = clips;
        _controllers = controllers;
        _prefabs = prefabs;
    }

    public bool TryGetController(string controllerId, out AnimatorController controller)
        => _controllers.TryGetValue(controllerId, out controller!);


    public bool TryGetSprite(string spriteId, out SpriteDefinition sprite)
        => _sprites.TryGetValue(spriteId, out sprite!);

    public bool TryGetAnimation(string clipId, out AnimationClip clip)
        => _clips.TryGetValue(clipId, out clip!);

    public bool TryGetPrefab(string prefabId, out Prefab prefab)
        => _prefabs.TryGetValue(prefabId, out prefab!);
}
