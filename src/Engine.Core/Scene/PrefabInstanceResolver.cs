using Engine.Core.Assets;
using Engine.Core.Components;

namespace Engine.Core.Scene;

public static class PrefabInstanceResolver
{
    public static void Apply(Scene scene, IAssetProvider assets)
    {
        for (int i = 0; i < scene.Entities.Count; i++)
        {
            var entity = scene.Entities[i];
            if (!entity.TryGet<PrefabInstance>(out var instance) || instance is null)
                continue;

            if (string.IsNullOrWhiteSpace(instance.PrefabId))
                continue;

            if (!assets.TryGetPrefab(instance.PrefabId, out var prefab))
                continue;

            var root = prefab.GetRootEntity();

            if (instance.UsePrefabTransform)
            {
                entity.Transform.Position = root.Position;
                entity.Transform.Scale = root.Scale;
                entity.Transform.Rotation = System.Numerics.Quaternion.CreateFromAxisAngle(
                    System.Numerics.Vector3.UnitZ,
                    root.RotationZRadians);
            }

            if (!instance.OverrideSpriteRenderer && root.SpriteRenderer is not null)
                entity.Add(root.SpriteRenderer.ToComponent());

            if (!instance.OverrideAnimator && root.Animator is not null)
                entity.Add(root.Animator.ToComponent());

            if (!instance.OverrideBoxCollider2D && root.BoxCollider2D is not null)
                entity.Add(root.BoxCollider2D.ToComponent());

            if (!instance.OverridePhysicsBody2D && root.PhysicsBody2D is not null)
                entity.Add(root.PhysicsBody2D.ToComponent());

            if (!instance.OverrideRigidbody2D && root.Rigidbody2D is not null)
                entity.Add(root.Rigidbody2D.ToComponent());
        }
    }
}
