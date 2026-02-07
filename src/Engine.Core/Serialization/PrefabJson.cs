using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Components;
using Engine.Core.Math;
using Engine.Core.Rendering;
using Engine.Core.Scene;

namespace Engine.Core.Serialization;

public static class PrefabJson
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(Prefab prefab, int version = CurrentVersion)
    {
        if (version > CurrentVersion)
            throw new NotSupportedException($"Unsupported prefab version: {version}");

        var dto = PrefabDto.FromPrefab(prefab, version);
        return JsonSerializer.Serialize(dto, Options);
    }

    public static Prefab Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<PrefabDto>(json, Options)
                  ?? throw new InvalidOperationException("Prefab JSON deserialized to null.");

        if (dto.Version > CurrentVersion)
            throw new NotSupportedException($"Unsupported prefab version: {dto.Version}");

        if (dto.Version < CurrentVersion)
            dto = PrefabDto.UpgradeToCurrent(dto);

        return dto.ToPrefab();
    }

    private sealed class PrefabDto
    {
        public int Version { get; set; } = 1;
        public Guid RootId { get; set; }
        public List<EntityDto> Entities { get; set; } = new();

        public static PrefabDto FromPrefab(Prefab prefab, int version)
        {
            return new PrefabDto
            {
                Version = version,
                RootId = prefab.RootId,
                Entities = prefab.Entities
                    .OrderBy(e => e.Id)
                    .Select(EntityDto.FromPrefabEntity)
                    .ToList()
            };
        }

        public static PrefabDto UpgradeToCurrent(PrefabDto dto)
        {
            // Placeholder for future prefab migrations.
            return dto;
        }

        public Prefab ToPrefab()
        {
            var entities = Entities.Select(e => e.ToPrefabEntity()).ToList();
            if (entities.Count == 0)
                throw new InvalidOperationException("Prefab JSON contained no entities.");

            if (entities.All(e => e.Id != RootId))
                throw new InvalidOperationException("Prefab JSON RootId did not match any entity.");

            return new Prefab(entities, RootId);
        }
    }

    private sealed class EntityDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "Entity";
        public TransformDto Transform { get; set; } = new();

        public SpriteRendererDto? SpriteRenderer { get; set; }
        public AnimatorDto? Animator { get; set; }
        public BoxCollider2DDto? BoxCollider2D { get; set; }
        public PhysicsBody2DDto? PhysicsBody2D { get; set; }
        public Rigidbody2DDto? Rigidbody2D { get; set; }
        public DebugRender2DDto? DebugRender2D { get; set; }

        public static EntityDto FromPrefabEntity(Prefab.PrefabEntity entity)
        {
            return new EntityDto
            {
                Id = entity.Id,
                Name = entity.Name,
                Transform = new TransformDto
                {
                    Position = new[] { entity.Position.X, entity.Position.Y, entity.Position.Z },
                    Scale = new[] { entity.Scale.X, entity.Scale.Y, entity.Scale.Z },
                RotationZDegrees = RadToDeg(entity.RotationZRadians)
            },
            SpriteRenderer = entity.SpriteRenderer is null ? null : SpriteRendererDto.FromData(entity.SpriteRenderer),
            Animator = entity.Animator is null ? null : AnimatorDto.FromData(entity.Animator),
            BoxCollider2D = entity.BoxCollider2D is null ? null : BoxCollider2DDto.FromData(entity.BoxCollider2D),
            PhysicsBody2D = entity.PhysicsBody2D is null ? null : PhysicsBody2DDto.FromData(entity.PhysicsBody2D),
            Rigidbody2D = entity.Rigidbody2D is null ? null : Rigidbody2DDto.FromData(entity.Rigidbody2D),
            DebugRender2D = entity.DebugRender2D is null ? null : DebugRender2DDto.FromData(entity.DebugRender2D)
        };
    }

        public Prefab.PrefabEntity ToPrefabEntity()
        {
            return new Prefab.PrefabEntity
            {
                Id = Id,
                Name = Name,
                Position = new Vector3(Transform.Position[0], Transform.Position[1], Transform.Position[2]),
                Scale = new Vector3(Transform.Scale[0], Transform.Scale[1], Transform.Scale[2]),
                RotationZRadians = GetRotationRadians(Transform),
                SpriteRenderer = SpriteRenderer is null ? null : SpriteRenderer.ToData(),
                Animator = Animator is null ? null : Animator.ToData(),
                BoxCollider2D = BoxCollider2D is null ? null : BoxCollider2D.ToData(),
                PhysicsBody2D = PhysicsBody2D is null ? null : PhysicsBody2D.ToData(),
                Rigidbody2D = Rigidbody2D is null ? null : Rigidbody2D.ToData(),
                DebugRender2D = DebugRender2D is null ? null : DebugRender2D.ToData()
            };
        }
    }

    private sealed class TransformDto
    {
        public float[] Position { get; set; } = new float[] { 0, 0, 0 };

        [JsonPropertyName("rotationZDegrees")]
        public float RotationZDegrees { get; set; } = 0f;

        [JsonPropertyName("rotationZRadians")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public float RotationZRadians { get; set; } = 0f;

        public float[] Scale { get; set; } = new float[] { 1, 1, 1 };
    }

    private sealed class SpriteRendererDto
    {
        public string? SpriteId { get; set; }
        public float[] Tint { get; set; } = new float[] { 1, 1, 1, 1 };
        public int Layer { get; set; } = 0;
        public bool OverrideSourceRect { get; set; } = false;
        public int[] SourceRect { get; set; } = new int[] { 0, 0, 0, 0 };
        public bool OverridePixelsPerUnit { get; set; } = false;
        public float PixelsPerUnitOverride { get; set; } = 100f;
        public SpriteFlip Flip { get; set; } = SpriteFlip.None;

        public static SpriteRendererDto FromData(Prefab.SpriteRendererData data)
        {
            return new SpriteRendererDto
            {
                SpriteId = data.SpriteId,
                Tint = new[] { data.Tint.R, data.Tint.G, data.Tint.B, data.Tint.A },
                Layer = data.Layer,
                OverrideSourceRect = data.OverrideSourceRect,
                SourceRect = new[] { data.SourceRectOverride.X, data.SourceRectOverride.Y, data.SourceRectOverride.W, data.SourceRectOverride.H },
                OverridePixelsPerUnit = data.OverridePixelsPerUnit,
                PixelsPerUnitOverride = data.PixelsPerUnitOverride,
                Flip = data.Flip
            };
        }

        public Prefab.SpriteRendererData ToData()
        {
            return new Prefab.SpriteRendererData
            {
                SpriteId = SpriteId ?? string.Empty,
                Tint = new Color4(Tint[0], Tint[1], Tint[2], Tint[3]),
                Layer = Layer,
                OverrideSourceRect = OverrideSourceRect,
                SourceRectOverride = new IntRect(SourceRect[0], SourceRect[1], SourceRect[2], SourceRect[3]),
                OverridePixelsPerUnit = OverridePixelsPerUnit,
                PixelsPerUnitOverride = PixelsPerUnitOverride,
                Flip = Flip
            };
        }
    }

    private sealed class AnimatorDto
    {
        public string? ControllerId { get; set; }
        public string? ClipId { get; set; }
        public bool Playing { get; set; } = true;
        public float Speed { get; set; } = 1f;
        public bool LoopOverride { get; set; } = false;
        public bool Loop { get; set; } = true;
        public int FrameIndex { get; set; } = 0;
        public float TimeIntoFrame { get; set; } = 0f;
        public float DefaultCrossFadeSeconds { get; set; } = 0.04f;
        public bool DefaultFreezeDuringCrossFade { get; set; } = false;

        public static AnimatorDto FromData(Prefab.AnimatorData data)
        {
            return new AnimatorDto
            {
                ControllerId = data.ControllerId,
                ClipId = data.ClipId,
                Playing = data.Playing,
                Speed = data.Speed,
                LoopOverride = data.LoopOverride,
                Loop = data.Loop,
                FrameIndex = data.FrameIndex,
                TimeIntoFrame = data.TimeIntoFrame,
                DefaultCrossFadeSeconds = data.DefaultCrossFadeSeconds,
                DefaultFreezeDuringCrossFade = data.DefaultFreezeDuringCrossFade
            };
        }

        public Prefab.AnimatorData ToData()
        {
            return new Prefab.AnimatorData
            {
                ControllerId = ControllerId ?? string.Empty,
                ClipId = ClipId ?? string.Empty,
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

    private sealed class BoxCollider2DDto
    {
        public float[] Size { get; set; } = new float[] { 1, 1 };
        public float[] Offset { get; set; } = new float[] { 0, 0 };
        public bool IsTrigger { get; set; } = false;

        public static BoxCollider2DDto FromData(Prefab.BoxCollider2DData data)
        {
            return new BoxCollider2DDto
            {
                Size = new[] { data.Size.X, data.Size.Y },
                Offset = new[] { data.Offset.X, data.Offset.Y },
                IsTrigger = data.IsTrigger
            };
        }

        public Prefab.BoxCollider2DData ToData()
        {
            return new Prefab.BoxCollider2DData
            {
                Size = new Vector2(Size[0], Size[1]),
                Offset = new Vector2(Offset[0], Offset[1]),
                IsTrigger = IsTrigger
            };
        }
    }

    private sealed class PhysicsBody2DDto
    {
        public bool IsStatic { get; set; } = false;

        public static PhysicsBody2DDto FromData(Prefab.PhysicsBody2DData data)
        {
            return new PhysicsBody2DDto
            {
                IsStatic = data.IsStatic
            };
        }

        public Prefab.PhysicsBody2DData ToData()
        {
            return new Prefab.PhysicsBody2DData
            {
                IsStatic = IsStatic
            };
        }
    }

    private sealed class Rigidbody2DDto
    {
        public float Mass { get; set; } = 1f;
        public float[] Velocity { get; set; } = new float[] { 0, 0 };
        public bool UseGravity { get; set; } = true;
        public float GravityScale { get; set; } = 1f;
        public float LinearDrag { get; set; } = 0f;
        public float Friction { get; set; } = 0.2f;

        public static Rigidbody2DDto FromData(Prefab.Rigidbody2DData data)
        {
            return new Rigidbody2DDto
            {
                Mass = data.Mass,
                Velocity = new[] { data.Velocity.X, data.Velocity.Y },
                UseGravity = data.UseGravity,
                GravityScale = data.GravityScale,
                LinearDrag = data.LinearDrag,
                Friction = data.Friction
            };
        }

        public Prefab.Rigidbody2DData ToData()
        {
            return new Prefab.Rigidbody2DData
            {
                Mass = Mass,
                Velocity = new Vector2(Velocity[0], Velocity[1]),
                UseGravity = UseGravity,
                GravityScale = GravityScale,
                LinearDrag = LinearDrag,
                Friction = Friction
            };
        }
    }

    private sealed class DebugRender2DDto
    {
        public bool ShowCollider { get; set; } = false;

        public static DebugRender2DDto FromData(Prefab.DebugRender2DData data)
        {
            return new DebugRender2DDto
            {
                ShowCollider = data.ShowCollider
            };
        }

        public Prefab.DebugRender2DData ToData()
        {
            return new Prefab.DebugRender2DData
            {
                ShowCollider = ShowCollider
            };
        }
    }

    private static float GetRotationRadians(TransformDto t)
    {
        if (t.RotationZDegrees != 0f)
            return DegToRad(t.RotationZDegrees);

        return t.RotationZRadians;
    }

    private static float DegToRad(float degrees) => degrees * (MathF.PI / 180f);
    private static float RadToDeg(float radians) => radians * (180f / MathF.PI);
}
