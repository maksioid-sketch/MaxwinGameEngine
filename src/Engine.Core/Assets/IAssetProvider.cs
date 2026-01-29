namespace Engine.Core.Assets;

public interface IAssetProvider
{
    bool TryGetSprite(string spriteId, out SpriteDefinition sprite);
}
