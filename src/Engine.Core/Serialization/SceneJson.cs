using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Engine.Core.Components;
using Engine.Core.Math;
using SceneType = Engine.Core.Scene.Scene;
using EntityType = Engine.Core.Scene.Entity;


namespace Engine.Core.Serialization;

public static class SceneJson
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(SceneType scene, int version = CurrentVersion)
    {
        if (version > CurrentVersion)
            throw new NotSupportedException($"Unsupported scene version: {version}");

        var dto = SceneDto.FromScene(scene, version);
        return JsonSerializer.Serialize(dto, Options);
    }

    public static SceneType Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<SceneDto>(json, Options)
                  ?? throw new InvalidOperationException("Scene JSON deserialized to null.");

        if (dto.Version > CurrentVersion)
            throw new NotSupportedException($"Unsupported scene version: {dto.Version}");

        if (dto.Version < CurrentVersion)
            dto = SceneDto.UpgradeToCurrent(dto);

        return dto.ToScene();
    }

    // ---------------- DTOs ----------------

    private sealed class SceneDto
    {
        public int Version { get; set; } = 1;
        public List<EntityDto> Entities { get; set; } = new();

        public static SceneDto FromScene(SceneType scene, int version)
        {
            return new SceneDto
            {
                Version = version,
                Entities = scene.Entities
                    .OrderBy(e => e.Id)
                    .Select(EntityDto.FromEntity)
                    .ToList()
            };
        }

        public static SceneDto UpgradeToCurrent(SceneDto dto)
        {
            // Placeholder for future scene migrations.
            return dto;
        }

        public SceneType ToScene()
        {
            var scene = new SceneType();
            foreach (var e in Entities)
            {
                var entity = scene.CreateEntity(e.Id, e.Name);

                if (!string.IsNullOrWhiteSpace(e.PrefabId))
                {
                    entity.Add(new PrefabInstance
                    {
                        PrefabId = e.PrefabId,
                        UsePrefabTransform = e.Transform is null,
                        OverrideTransform = e.Transform is not null,
                        OverrideSpriteRenderer = e.SpriteRenderer is not null,
                        OverrideAnimator = e.Animator is not null,
                        OverrideBoxCollider2D = e.BoxCollider2D is not null,
                        OverridePhysicsBody2D = e.PhysicsBody2D is not null,
                        OverrideRigidbody2D = e.Rigidbody2D is not null,
                        OverrideDebugRender2D = e.DebugRender2D is not null
                    });
                }

                if (e.Transform is not null)
                {
                    // Transform
                    entity.Transform.Position = new Vector3(e.Transform.Position[0], e.Transform.Position[1], e.Transform.Position[2]);
                    entity.Transform.Scale = new Vector3(e.Transform.Scale[0], e.Transform.Scale[1], e.Transform.Scale[2]);

                    // 2D rotation (around Z) stored as degrees in JSON
                    var rotRadians = GetRotationRadians(e.Transform);
                    entity.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, rotRadians);
                }

                // Components
                if (e.SpriteRenderer is not null)
                {
                    entity.Add(new SpriteRenderer
                    {
                        SpriteId = e.SpriteRenderer.SpriteId ?? "",
                        Layer = e.SpriteRenderer.Layer,
                        Tint = new Color4(
                            e.SpriteRenderer.Tint[0],
                            e.SpriteRenderer.Tint[1],
                            e.SpriteRenderer.Tint[2],
                            e.SpriteRenderer.Tint[3]),
                        
                    });
                }
                if (e.Animator is not null)
                {
                    entity.Add(new Engine.Core.Components.Animator
                    {
                        ControllerId = e.Animator.ControllerId ?? "",
                        ClipId = e.Animator.ClipId ?? "",
                        Playing = e.Animator.Playing,
                        Speed = e.Animator.Speed,
                        LoopOverride = e.Animator.LoopOverride,
                        Loop = e.Animator.Loop,
                        FrameIndex = e.Animator.FrameIndex,
                        TimeIntoFrame = e.Animator.TimeIntoFrame,
                        DefaultCrossFadeSeconds = e.Animator.DefaultCrossFadeSeconds,
                        DefaultFreezeDuringCrossFade = e.Animator.DefaultFreezeDuringCrossFade,

                    });
                }
                if (e.BoxCollider2D is not null)
                {
                    entity.Add(new BoxCollider2D
                    {
                        Size = new Vector2(e.BoxCollider2D.Size[0], e.BoxCollider2D.Size[1]),
                        Offset = new Vector2(e.BoxCollider2D.Offset[0], e.BoxCollider2D.Offset[1]),
                        IsTrigger = e.BoxCollider2D.IsTrigger
                    });
                }

                if (e.PhysicsBody2D is not null)
                {
                    entity.Add(new PhysicsBody2D
                    {
                        IsStatic = e.PhysicsBody2D.IsStatic
                    });
                }

                if (e.Rigidbody2D is not null)
                {
                    entity.Add(new Rigidbody2D
                    {
                        Mass = e.Rigidbody2D.Mass,
                        Velocity = new Vector2(e.Rigidbody2D.Velocity[0], e.Rigidbody2D.Velocity[1]),
                        UseGravity = e.Rigidbody2D.UseGravity,
                        GravityScale = e.Rigidbody2D.GravityScale,
                        LinearDrag = e.Rigidbody2D.LinearDrag,
                        Friction = e.Rigidbody2D.Friction
                    });
                }

                if (e.DebugRender2D is not null)
                {
                    entity.Add(new DebugRender2D
                    {
                        ShowCollider = e.DebugRender2D.ShowCollider
                    });
                }

            }

            return scene;
        }
    }

    private sealed class EntityDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "Entity";
        public string? PrefabId { get; set; }
        public TransformDto? Transform { get; set; }

        // Optional components (add more later)
        public SpriteRendererDto? SpriteRenderer { get; set; }
        public AnimatorDto? Animator { get; set; }
        public BoxCollider2DDto? BoxCollider2D { get; set; }
        public PhysicsBody2DDto? PhysicsBody2D { get; set; }
        public Rigidbody2DDto? Rigidbody2D { get; set; }
        public DebugRender2DDto? DebugRender2D { get; set; }


        public static EntityDto FromEntity(EntityType e)
        {
            e.TryGet<SpriteRenderer>(out var spr);

            e.TryGet<Engine.Core.Components.Animator>(out var anim);
            e.TryGet<BoxCollider2D>(out var box);
            e.TryGet<PhysicsBody2D>(out var body);
            e.TryGet<Rigidbody2D>(out var rb);
            e.TryGet<DebugRender2D>(out var debug);
            e.TryGet<PrefabInstance>(out var pi);
            
            


            var rotZ = GetZRotationRadians(e.Transform.Rotation);

            bool includeTransform = pi is null || !pi.UsePrefabTransform || pi.OverrideTransform;
            bool includeSpriteRenderer = pi is null || pi.OverrideSpriteRenderer;
            bool includeAnimator = pi is null || pi.OverrideAnimator;
            bool includeBoxCollider2D = pi is null || pi.OverrideBoxCollider2D;
            bool includePhysicsBody2D = pi is null || pi.OverridePhysicsBody2D;
            bool includeRigidbody2D = pi is null || pi.OverrideRigidbody2D;
            bool includeDebugRender2D = pi is null || pi.OverrideDebugRender2D;

            return new EntityDto
            {
                Id = e.Id,
                Name = e.Name,
                PrefabId = pi is not null ? pi.PrefabId : null,
                Transform = includeTransform ? new TransformDto
                {
                    Position = new[] { e.Transform.Position.X, e.Transform.Position.Y, e.Transform.Position.Z },
                    Scale = new[] { e.Transform.Scale.X, e.Transform.Scale.Y, e.Transform.Scale.Z },
                    RotationZDegrees = RadToDeg(rotZ)
                } : null,
                SpriteRenderer = !includeSpriteRenderer || spr is null ? null : new SpriteRendererDto
                {
                    SpriteId = spr.SpriteId,
                    Layer = spr.Layer,
                    Tint = new[] { spr.Tint.R, spr.Tint.G, spr.Tint.B, spr.Tint.A },
                },
                Animator = !includeAnimator || anim is null ? null : new AnimatorDto
                {
                    ControllerId = anim.ControllerId,
                    ClipId = anim.ClipId,
                    Playing = anim.Playing,
                    Speed = anim.Speed,
                    LoopOverride = anim.LoopOverride,
                    Loop = anim.Loop,
                    FrameIndex = anim.FrameIndex,
                    TimeIntoFrame = anim.TimeIntoFrame,
                    DefaultCrossFadeSeconds = anim.DefaultCrossFadeSeconds,
                    DefaultFreezeDuringCrossFade = anim.DefaultFreezeDuringCrossFade
                },
                BoxCollider2D = !includeBoxCollider2D || box is null ? null : new BoxCollider2DDto
                {
                    Size = new[] { box.Size.X, box.Size.Y },
                    Offset = new[] { box.Offset.X, box.Offset.Y },
                    IsTrigger = box.IsTrigger
                },
                PhysicsBody2D = !includePhysicsBody2D || body is null ? null : new PhysicsBody2DDto
                {
                    IsStatic = body.IsStatic
                },
                Rigidbody2D = !includeRigidbody2D || rb is null ? null : new Rigidbody2DDto
                {
                    Mass = rb.Mass,
                    Velocity = new[] { rb.Velocity.X, rb.Velocity.Y },
                    UseGravity = rb.UseGravity,
                    GravityScale = rb.GravityScale,
                    LinearDrag = rb.LinearDrag,
                    Friction = rb.Friction
                },
                DebugRender2D = !includeDebugRender2D || debug is null ? null : new DebugRender2DDto
                {
                    ShowCollider = debug.ShowCollider
                }
            };
        }

        private static float GetZRotationRadians(Quaternion q)
        {
            var siny_cosp = 2f * (q.W * q.Z + q.X * q.Y);
            var cosy_cosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
            return (float)System.Math.Atan2(siny_cosp, cosy_cosp);
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
        public int Layer { get; set; } = 0;
        
        // RGBA floats
        public float[] Tint { get; set; } = new float[] { 1, 1, 1, 1 };
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

    }

    private sealed class BoxCollider2DDto
    {
        public float[] Size { get; set; } = new float[] { 1, 1 };
        public float[] Offset { get; set; } = new float[] { 0, 0 };
        public bool IsTrigger { get; set; } = false;
    }

    private sealed class PhysicsBody2DDto
    {
        public bool IsStatic { get; set; } = false;
    }

    private sealed class Rigidbody2DDto
    {
        public float Mass { get; set; } = 1f;
        public float[] Velocity { get; set; } = new float[] { 0, 0 };
        public bool UseGravity { get; set; } = true;
        public float GravityScale { get; set; } = 1f;
        public float LinearDrag { get; set; } = 0f;
        public float Friction { get; set; } = 0.2f;
    }

    private sealed class DebugRender2DDto
    {
        public bool ShowCollider { get; set; } = false;
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
