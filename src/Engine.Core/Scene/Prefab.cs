using System.Numerics;
using Engine.Core.Components;
using Engine.Core.Math;
using Engine.Core.Rendering;

namespace Engine.Core.Scene;

public sealed class Prefab
{
    private readonly List<PrefabEntity> _entities;

    public Guid RootId { get; }
    public IReadOnlyList<PrefabEntity> Entities => _entities;

    internal Prefab(List<PrefabEntity> entities, Guid rootId)
    {
        if (entities.Count == 0)
            throw new ArgumentException("Prefab must contain at least one entity.", nameof(entities));

        _entities = entities;
        RootId = rootId;
    }

    public PrefabEntity GetRootEntity()
    {
        var root = _entities.FirstOrDefault(e => e.Id == RootId);
        if (root is null)
            throw new InvalidOperationException("Prefab root entity was not found.");
        return root;
    }

    public static Prefab FromScene(Scene scene, Guid rootId)
    {
        if (scene.Entities.Count == 0)
            throw new InvalidOperationException("Cannot create prefab from an empty scene.");

        var entities = scene.Entities.Select(PrefabEntity.FromEntity).ToList();
        if (entities.All(e => e.Id != rootId))
            throw new InvalidOperationException("Root entity ID was not found in the scene.");

        return new Prefab(entities, rootId);
    }

    public Entity Instantiate(Scene scene, Vector3? positionOverride = null)
    {
        var idMap = new Dictionary<Guid, Entity>(_entities.Count);

        var rootEntity = _entities.FirstOrDefault(e => e.Id == RootId);
        var rootPosition = rootEntity?.Position ?? Vector3.Zero;
        var offset = positionOverride.HasValue ? positionOverride.Value - rootPosition : Vector3.Zero;

        foreach (var prefabEntity in _entities)
        {
            var entity = scene.CreateEntity(prefabEntity.Name);
            idMap[prefabEntity.Id] = entity;

            entity.Transform.Position = prefabEntity.Position + offset;
            entity.Transform.Scale = prefabEntity.Scale;
            entity.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, prefabEntity.RotationZRadians);

            if (prefabEntity.SpriteRenderer is not null)
                entity.Add(prefabEntity.SpriteRenderer.ToComponent());

            if (prefabEntity.Animator is not null)
                entity.Add(prefabEntity.Animator.ToComponent());

            if (prefabEntity.BoxCollider2D is not null)
                entity.Add(prefabEntity.BoxCollider2D.ToComponent());

            if (prefabEntity.PhysicsBody2D is not null)
                entity.Add(prefabEntity.PhysicsBody2D.ToComponent());

            if (prefabEntity.Rigidbody2D is not null)
                entity.Add(prefabEntity.Rigidbody2D.ToComponent());
        }

        return idMap.TryGetValue(RootId, out var root) ? root : idMap.Values.First();
    }

    public sealed class PrefabEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "Entity";
        public Vector3 Position { get; set; } = Vector3.Zero;
        public Vector3 Scale { get; set; } = Vector3.One;
        public float RotationZRadians { get; set; } = 0f;

        public SpriteRendererData? SpriteRenderer { get; set; }
        public AnimatorData? Animator { get; set; }
        public BoxCollider2DData? BoxCollider2D { get; set; }
        public PhysicsBody2DData? PhysicsBody2D { get; set; }
        public Rigidbody2DData? Rigidbody2D { get; set; }

        public static PrefabEntity FromEntity(Entity entity)
        {
            entity.TryGet<SpriteRenderer>(out var spr);
            entity.TryGet<Components.Animator>(out var anim);
            entity.TryGet<BoxCollider2D>(out var box);
            entity.TryGet<PhysicsBody2D>(out var body);
            entity.TryGet<Rigidbody2D>(out var rb);

            return new PrefabEntity
            {
                Id = entity.Id,
                Name = entity.Name,
                Position = entity.Transform.Position,
                Scale = entity.Transform.Scale,
                RotationZRadians = GetZRotationRadians(entity.Transform.Rotation),
                SpriteRenderer = spr is null ? null : SpriteRendererData.FromComponent(spr),
                Animator = anim is null ? null : AnimatorData.FromComponent(anim),
                BoxCollider2D = box is null ? null : BoxCollider2DData.FromComponent(box),
                PhysicsBody2D = body is null ? null : PhysicsBody2DData.FromComponent(body),
                Rigidbody2D = rb is null ? null : Rigidbody2DData.FromComponent(rb)
            };
        }

        private static float GetZRotationRadians(Quaternion q)
        {
            var siny_cosp = 2f * (q.W * q.Z + q.X * q.Y);
            var cosy_cosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
            return (float)System.Math.Atan2(siny_cosp, cosy_cosp);
        }
    }

    public sealed class SpriteRendererData
    {
        public string SpriteId { get; set; } = string.Empty;
        public Color4 Tint { get; set; } = Color4.White;
        public int Layer { get; set; } = 0;
        public bool OverrideSourceRect { get; set; } = false;
        public IntRect SourceRectOverride { get; set; } = new(0, 0, 0, 0);
        public bool OverridePixelsPerUnit { get; set; } = false;
        public float PixelsPerUnitOverride { get; set; } = 100f;
        public SpriteFlip Flip { get; set; } = SpriteFlip.None;

        public static SpriteRendererData FromComponent(SpriteRenderer renderer)
        {
            return new SpriteRendererData
            {
                SpriteId = renderer.SpriteId,
                Tint = renderer.Tint,
                Layer = renderer.Layer,
                OverrideSourceRect = renderer.OverrideSourceRect,
                SourceRectOverride = renderer.SourceRectOverride,
                OverridePixelsPerUnit = renderer.OverridePixelsPerUnit,
                PixelsPerUnitOverride = renderer.PixelsPerUnitOverride,
                Flip = renderer.Flip
            };
        }

        public SpriteRenderer ToComponent()
        {
            return new SpriteRenderer
            {
                SpriteId = SpriteId,
                Tint = Tint,
                Layer = Layer,
                OverrideSourceRect = OverrideSourceRect,
                SourceRectOverride = SourceRectOverride,
                OverridePixelsPerUnit = OverridePixelsPerUnit,
                PixelsPerUnitOverride = PixelsPerUnitOverride,
                Flip = Flip
            };
        }
    }

    public sealed class AnimatorData
    {
        public string ControllerId { get; set; } = string.Empty;
        public string ClipId { get; set; } = string.Empty;
        public bool Playing { get; set; } = true;
        public float Speed { get; set; } = 1f;
        public bool LoopOverride { get; set; } = false;
        public bool Loop { get; set; } = true;
        public int FrameIndex { get; set; } = 0;
        public float TimeIntoFrame { get; set; } = 0f;
        public float DefaultCrossFadeSeconds { get; set; } = 0.04f;
        public bool DefaultFreezeDuringCrossFade { get; set; } = false;

        public static AnimatorData FromComponent(Components.Animator animator)
        {
            return new AnimatorData
            {
                ControllerId = animator.ControllerId,
                ClipId = animator.ClipId,
                Playing = animator.Playing,
                Speed = animator.Speed,
                LoopOverride = animator.LoopOverride,
                Loop = animator.Loop,
                FrameIndex = animator.FrameIndex,
                TimeIntoFrame = animator.TimeIntoFrame,
                DefaultCrossFadeSeconds = animator.DefaultCrossFadeSeconds,
                DefaultFreezeDuringCrossFade = animator.DefaultFreezeDuringCrossFade
            };
        }

        public Components.Animator ToComponent()
        {
            return new Components.Animator
            {
                ControllerId = ControllerId,
                ClipId = ClipId,
                Playing = Playing,
                Speed = Speed,
                LoopOverride = LoopOverride,
                Loop = Loop,
                FrameIndex = FrameIndex,
                TimeIntoFrame = TimeIntoFrame,
                DefaultCrossFadeSeconds = DefaultCrossFadeSeconds,
                DefaultFreezeDuringCrossFade = DefaultFreezeDuringCrossFade
            };
        }
    }

    public sealed class BoxCollider2DData
    {
        public Vector2 Size { get; set; } = new(1f, 1f);
        public Vector2 Offset { get; set; } = Vector2.Zero;
        public bool IsTrigger { get; set; } = false;

        public static BoxCollider2DData FromComponent(BoxCollider2D collider)
        {
            return new BoxCollider2DData
            {
                Size = collider.Size,
                Offset = collider.Offset,
                IsTrigger = collider.IsTrigger
            };
        }

        public BoxCollider2D ToComponent()
        {
            return new BoxCollider2D
            {
                Size = Size,
                Offset = Offset,
                IsTrigger = IsTrigger
            };
        }
    }

    public sealed class PhysicsBody2DData
    {
        public bool IsStatic { get; set; } = false;

        public static PhysicsBody2DData FromComponent(PhysicsBody2D body)
        {
            return new PhysicsBody2DData
            {
                IsStatic = body.IsStatic
            };
        }

        public PhysicsBody2D ToComponent()
        {
            return new PhysicsBody2D
            {
                IsStatic = IsStatic
            };
        }
    }

    public sealed class Rigidbody2DData
    {
        public float Mass { get; set; } = 1f;
        public Vector2 Velocity { get; set; } = Vector2.Zero;
        public bool UseGravity { get; set; } = true;
        public float GravityScale { get; set; } = 1f;
        public float LinearDrag { get; set; } = 0f;
        public float Friction { get; set; } = 0.2f;

        public static Rigidbody2DData FromComponent(Rigidbody2D body)
        {
            return new Rigidbody2DData
            {
                Mass = body.Mass,
                Velocity = body.Velocity,
                UseGravity = body.UseGravity,
                GravityScale = body.GravityScale,
                LinearDrag = body.LinearDrag,
                Friction = body.Friction
            };
        }

        public Rigidbody2D ToComponent()
        {
            return new Rigidbody2D
            {
                Mass = Mass,
                Velocity = Velocity,
                UseGravity = UseGravity,
                GravityScale = GravityScale,
                LinearDrag = LinearDrag,
                Friction = Friction
            };
        }
    }
}
