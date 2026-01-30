using System.Numerics;
using System.Text.Json;
using Engine.Core.Assets;
using Engine.Core.Math;

namespace Engine.Core.Serialization;

public static class AtlasJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static Dictionary<string, SpriteDefinition> DeserializeSprites(string json)
    {
        // v2 supports versioning. If version missing, assume v1.
        using var doc = JsonDocument.Parse(json);
        int version = 1;
        if (doc.RootElement.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number)
            version = v.GetInt32();

        return version switch
        {
            1 => DeserializeV1(json),
            2 => DeserializeV2(json),
            _ => throw new NotSupportedException($"Unsupported atlas version: {version}")
        };
    }

    private static Dictionary<string, SpriteDefinition> DeserializeV1(string json)
    {
        var dto = JsonSerializer.Deserialize<AtlasV1Dto>(json, Options)
                  ?? throw new InvalidOperationException("Atlas JSON deserialized to null (v1).");

        var dict = new Dictionary<string, SpriteDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in dto.Sprites)
        {
            var s = kv.Value;
            dict[kv.Key] = new SpriteDefinition
            {
                TextureKey = s.TextureKey ?? "",
                SourceRect = new IntRect(s.SourceRect[0], s.SourceRect[1], s.SourceRect[2], s.SourceRect[3]),
                PixelsPerUnit = s.PixelsPerUnit,
                // v1 had no origin; default behavior = center
                OriginPixels = Vector2.Zero,
                DefaultOriginToCenter = true
            };
        }

        return dict;
    }

    private static Dictionary<string, SpriteDefinition> DeserializeV2(string json)
    {
        var dto = JsonSerializer.Deserialize<AtlasV2Dto>(json, Options)
                  ?? throw new InvalidOperationException("Atlas JSON deserialized to null (v2).");

        var dict = new Dictionary<string, SpriteDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in dto.Sprites)
        {
            var s = kv.Value;
            var origin = s.OriginPixels ?? new float[] { 0f, 0f };

            dict[kv.Key] = new SpriteDefinition
            {
                TextureKey = s.TextureKey ?? "",
                SourceRect = new IntRect(s.SourceRect[0], s.SourceRect[1], s.SourceRect[2], s.SourceRect[3]),
                PixelsPerUnit = s.PixelsPerUnit,
                OriginPixels = new Vector2(origin[0], origin[1]),
                DefaultOriginToCenter = s.DefaultOriginToCenter
            };
        }

        return dict;
    }

    // -------- DTOs --------

    private sealed class AtlasV1Dto
    {
        public Dictionary<string, SpriteV1Dto> Sprites { get; set; } = new();
    }

    private sealed class SpriteV1Dto
    {
        public string? TextureKey { get; set; }
        public int[] SourceRect { get; set; } = new[] { 0, 0, 0, 0 };
        public float PixelsPerUnit { get; set; } = 100f;
    }

    private sealed class AtlasV2Dto
    {
        public int Version { get; set; } = 2;
        public Dictionary<string, SpriteV2Dto> Sprites { get; set; } = new();
    }

    private sealed class SpriteV2Dto
    {
        public string? TextureKey { get; set; }
        public int[] SourceRect { get; set; } = new[] { 0, 0, 0, 0 };
        public float PixelsPerUnit { get; set; } = 100f;

        // [x,y] in source pixels (relative to SourceRect top-left)
        public float[]? OriginPixels { get; set; } = null;

        public bool DefaultOriginToCenter { get; set; } = true;
    }
}
