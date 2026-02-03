using System.Text.Json;
using Engine.Core.Assets.Animation;

namespace Engine.Core.Serialization;

public static class AnimationJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static Dictionary<string, AnimationClip> DeserializeClips(string json)
    {
        var dto = JsonSerializer.Deserialize<AnimationsDto>(json, Options)
                  ?? throw new InvalidOperationException("Animations JSON deserialized to null.");

        var dict = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in dto.Clips)
        {
            var clipId = kv.Key;
            var c = kv.Value;

            var clip = new AnimationClip
            {
                Id = clipId,
                Loop = c.Loop,
                Frames = c.Frames.Select(f => new AnimationFrame(
                    f.SpriteId ?? "",
                    f.DurationSeconds,
                    f.Events
                )).ToList()
            };

            dict[clipId] = clip;
        }

        return dict;
    }

    private sealed class AnimationsDto
    {
        public Dictionary<string, ClipDto> Clips { get; set; } = new();
    }

    private sealed class ClipDto
    {
        public bool Loop { get; set; } = true;
        public List<FrameDto> Frames { get; set; } = new();
    }

    private sealed class FrameDto
    {
        public string? SpriteId { get; set; }
        public float DurationSeconds { get; set; } = 0.1f;

        // NEW: optional notifiers per frame
        public string[]? Events { get; set; } = null;
    }
}
