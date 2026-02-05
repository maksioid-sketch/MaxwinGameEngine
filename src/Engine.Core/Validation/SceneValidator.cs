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
            if (e.TryGet<PrefabInstance>(out var pi) && pi is not null)
            {
                if (string.IsNullOrWhiteSpace(pi.PrefabId))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Code = "PREFAB_ID_EMPTY",
                        EntityName = e.Name,
                        Message = "PrefabInstance.PrefabId is empty."
                    });
                }
                else if (!assets.TryGetPrefab(pi.PrefabId, out var prefab))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Code = "PREFAB_NOT_FOUND",
                        EntityName = e.Name,
                        Message = $"Prefab '{pi.PrefabId}' not found."
                    });
                }
                else
                {
                    try
                    {
                        _ = prefab.GetRootEntity();
                    }
                    catch (Exception ex)
                    {
                        issues.Add(new ValidationIssue
                        {
                            Severity = ValidationSeverity.Error,
                            Code = "PREFAB_ROOT_INVALID",
                            EntityName = e.Name,
                            Message = $"Prefab '{pi.PrefabId}' root invalid: {ex.Message}"
                        });
                    }
                }
            }

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
