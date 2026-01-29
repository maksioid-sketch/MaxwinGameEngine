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
        var dto = JsonSerializer.Deserialize<AtlasDto>(json, Options)
                  ?? throw new InvalidOperationException("Atlas JSON deserialized to null.");

        var dict = new Dictionary<string, SpriteDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in dto.Sprites)
        {
            var s = kv.Value;
            dict[kv.Key] = new SpriteDefinition
            {
                TextureKey = s.TextureKey ?? "",
                SourceRect = new IntRect(s.SourceRect[0], s.SourceRect[1], s.SourceRect[2], s.SourceRect[3]),
                PixelsPerUnit = s.PixelsPerUnit
            };
        }

        return dict;
    }

    private sealed class AtlasDto
    {
        public Dictionary<string, SpriteDto> Sprites { get; set; } = new();
    }

    private sealed class SpriteDto
    {
        public string? TextureKey { get; set; }
        public int[] SourceRect { get; set; } = new[] { 0, 0, 0, 0 };
        public float PixelsPerUnit { get; set; } = 100f;
    }
}
