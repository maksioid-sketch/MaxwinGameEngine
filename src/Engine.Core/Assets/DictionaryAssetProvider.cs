namespace Engine.Core.Assets;

public sealed class DictionaryAssetProvider : IAssetProvider
{
    private readonly Dictionary<string, SpriteDefinition> _sprites;

    public DictionaryAssetProvider(Dictionary<string, SpriteDefinition> sprites)
    {
        _sprites = sprites;
    }

    public bool TryGetSprite(string spriteId, out SpriteDefinition sprite)
        => _sprites.TryGetValue(spriteId, out sprite!);
}
