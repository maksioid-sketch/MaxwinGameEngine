using Engine.Core.Assets;
using Engine.Core.Components;
using Engine.Core.Scene;

namespace Engine.Core.Validation;

public static class SceneValidator
{
    public static List<ValidationIssue> Validate(Scene.Scene scene, IAssetProvider assets)
    {
        var issues = new List<ValidationIssue>();

        foreach (var e in scene.Entities)
        {
            if (!e.TryGet<SpriteRenderer>(out var sr) || sr is null)
                continue;

            if (string.IsNullOrWhiteSpace(sr.SpriteId))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SPRITE_ID_EMPTY",
                    EntityName = e.Name,
                    Message = "SpriteRenderer.SpriteId is empty (scene JSON likely wrong/outdated)."
                });
                continue;
            }

            if (!assets.TryGetSprite(sr.SpriteId, out var def))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "SPRITE_ID_NOT_IN_ATLAS",
                    EntityName = e.Name,
                    Message = $"SpriteId '{sr.SpriteId}' not found in atlas."
                });
                continue;
            }

            if (string.IsNullOrWhiteSpace(def.TextureKey))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "ATLAS_TEXTUREKEY_EMPTY",
                    EntityName = e.Name,
                    Message = $"Atlas sprite '{sr.SpriteId}' has empty textureKey."
                });
            }
        }

        return issues;
    }
}
