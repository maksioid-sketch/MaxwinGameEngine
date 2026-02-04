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
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(SceneType scene, int version = 1)
    {
        var dto = SceneDto.FromScene(scene, version);
        return JsonSerializer.Serialize(dto, Options);
    }

    public static SceneType Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<SceneDto>(json, Options)
                  ?? throw new InvalidOperationException("Scene JSON deserialized to null.");

        if (dto.Version != 1)
            throw new NotSupportedException($"Unsupported scene version: {dto.Version}");

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
                Entities = scene.Entities.Select(EntityDto.FromEntity).ToList()
            };
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
                        OverrideBoxCollider2D = e.BoxCollider2D is not null
                    });
                }

                if (e.Transform is not null)
                {
                    // Transform
                    entity.Transform.Position = new Vector3(e.Transform.Position[0], e.Transform.Position[1], e.Transform.Position[2]);
                    entity.Transform.Scale = new Vector3(e.Transform.Scale[0], e.Transform.Scale[1], e.Transform.Scale[2]);

                    // 2D rotation (around Z) stored as radians
                    entity.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, e.Transform.RotationZRadians);
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


        public static EntityDto FromEntity(EntityType e)
        {
            e.TryGet<SpriteRenderer>(out var spr);

            e.TryGet<Engine.Core.Components.Animator>(out var anim);
            e.TryGet<BoxCollider2D>(out var box);
            
            


            var rotZ = GetZRotationRadians(e.Transform.Rotation);

            return new EntityDto
            {
                Id = e.Id,
                Name = e.Name,
                PrefabId = e.TryGet<PrefabInstance>(out var pi) && pi is not null ? pi.PrefabId : null,
                Transform = new TransformDto
                {
                    Position = new[] { e.Transform.Position.X, e.Transform.Position.Y, e.Transform.Position.Z },
                    Scale = new[] { e.Transform.Scale.X, e.Transform.Scale.Y, e.Transform.Scale.Z },
                    RotationZRadians = rotZ
                },
                SpriteRenderer = spr is null ? null : new SpriteRendererDto
                {
                    SpriteId = spr.SpriteId,
                    Layer = spr.Layer,
                    Tint = new[] { spr.Tint.R, spr.Tint.G, spr.Tint.B, spr.Tint.A },
                },
                Animator = anim is null ? null : new AnimatorDto
                {
                    ClipId = anim.ClipId,
                    Playing = anim.Playing,
                    Speed = anim.Speed,
                    LoopOverride = anim.LoopOverride,
                    Loop = anim.Loop,
                    FrameIndex = anim.FrameIndex,
                    TimeIntoFrame = anim.TimeIntoFrame
                },
                BoxCollider2D = box is null ? null : new BoxCollider2DDto
                {
                    Size = new[] { box.Size.X, box.Size.Y },
                    Offset = new[] { box.Offset.X, box.Offset.Y },
                    IsTrigger = box.IsTrigger
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

}
