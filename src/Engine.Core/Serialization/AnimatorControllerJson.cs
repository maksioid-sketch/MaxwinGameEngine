using System.Text.Json;
using Engine.Core.Assets.Animation;
using System.Text.Json.Serialization;


namespace Engine.Core.Serialization;

public static class AnimatorControllerJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters =
    {
        // Allow enums like CompareOp to be read from strings: "Gt", "Le", etc.
        new JsonStringEnumConverter()
    }
    };



    public static Dictionary<string, AnimatorController> DeserializeControllers(string json)
    {
        var dto = JsonSerializer.Deserialize<ControllersFileDto>(json, Options)
                  ?? throw new InvalidOperationException("controllers.json deserialized to null.");

        return dto.Controllers ?? new Dictionary<string, AnimatorController>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ControllersFileDto
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, AnimatorController>? Controllers { get; set; }
    }
}
