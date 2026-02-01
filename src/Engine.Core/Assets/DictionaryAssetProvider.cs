using Engine.Core.Assets.Animation;

namespace Engine.Core.Assets;

public sealed class DictionaryAssetProvider : IAssetProvider
{
    private readonly Dictionary<string, SpriteDefinition> _sprites;
    private readonly Dictionary<string, AnimationClip> _clips;
    private readonly Dictionary<string, AnimatorController> _controllers;

    public DictionaryAssetProvider(
        Dictionary<string, SpriteDefinition> sprites,
        Dictionary<string, AnimationClip> clips,
        Dictionary<string, AnimatorController> controllers)
    {
        _sprites = sprites;
        _clips = clips;
        _controllers = controllers;
    }

    public bool TryGetController(string controllerId, out AnimatorController controller)
        => _controllers.TryGetValue(controllerId, out controller);


    public bool TryGetSprite(string spriteId, out SpriteDefinition sprite)
        => _sprites.TryGetValue(spriteId, out sprite!);

    public bool TryGetAnimation(string clipId, out AnimationClip clip)
        => _clips.TryGetValue(clipId, out clip!);
}
