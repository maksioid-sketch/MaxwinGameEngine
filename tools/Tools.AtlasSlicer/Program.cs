using System.Text.Json;
using System.Text.Json.Serialization;

Console.WriteLine("AtlasSlicer START");
Console.WriteLine("Args: " + string.Join(" | ", args));

if (args.Length < 2)
{
    Console.WriteLine("Usage: Tools.AtlasSlicer <input atlas_slices.json> <output atlas.generated.json> [output animations.generated.json]");
    Environment.ExitCode = 2;
    return;
}

var inputPath = Path.GetFullPath(args[0]);
var atlasOutPath = Path.GetFullPath(args[1]);
string? animOutPath = args.Length >= 3 ? Path.GetFullPath(args[2]) : null;


if (!File.Exists(inputPath))
{
    Console.WriteLine($"Input not found: {inputPath}");
    Environment.ExitCode = 3;
    return;
}

var opts = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true
};

SliceSpec spec;
try
{
    spec = JsonSerializer.Deserialize<SliceSpec>(File.ReadAllText(inputPath), opts)
           ?? throw new Exception("Slice spec deserialized to null.");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to parse slice spec: {ex.Message}");
    Environment.ExitCode = 4;
    return;
}

var atlas = new AtlasV2 { Version = 2 };

AnimationsFile? anims = animOutPath is null ? null : new AnimationsFile();


foreach (var sheet in spec.Sheets)
{
    if (sheet.FrameWidth <= 0 || sheet.FrameHeight <= 0)
        Fail($"Invalid frame size for sheet '{sheet.Name}'");

    if (sheet.Columns <= 0 || sheet.Rows <= 0)
        Fail($"Invalid grid size for sheet '{sheet.Name}'");

    var origin = sheet.OriginPixels ?? (sheet.DefaultOriginToCenter
        ? new float[] { sheet.FrameWidth * 0.5f, sheet.FrameHeight * 0.5f }
        : new float[] { 0f, 0f });

    // ---- Create clip ONCE per sheet ----
    AnimationClipDto? clipDto = null;
    float frameDuration = 0.1f;

    int clipStart = 0;
    int clipMaxCount = int.MaxValue;
    int clipFrameCounter = 0;

    if (anims != null && sheet.Clip != null && !string.IsNullOrWhiteSpace(sheet.Clip.ClipId))
    {
        if (sheet.Clip.Fps.HasValue && sheet.Clip.Fps.Value > 0f)
            frameDuration = 1f / sheet.Clip.Fps.Value;
        else if (sheet.Clip.DurationSeconds.HasValue && sheet.Clip.DurationSeconds.Value > 0f)
            frameDuration = sheet.Clip.DurationSeconds.Value;

        clipStart = sheet.Clip.StartIndex;
        clipMaxCount = sheet.Clip.FrameCount ?? int.MaxValue;

        clipDto = new AnimationClipDto { Loop = sheet.Clip.Loop };

        // IMPORTANT: only create/overwrite once here
        anims.Clips[sheet.Clip.ClipId] = clipDto;

        Console.WriteLine($"Sheet '{sheet.Name}': generating clip '{sheet.Clip.ClipId}' @ {frameDuration:0.###}s/frame");
    }
    else if (anims != null)
    {
        Console.WriteLine($"Sheet '{sheet.Name}': no clip spec found (clip is null or clipId empty).");
    }

    // ---- Generate sprites + append frames ----
    for (int r = 0; r < sheet.Rows; r++)
    {
        for (int c = 0; c < sheet.Columns; c++)
        {
            int index = r * sheet.Columns + c;

            if (sheet.FrameCount.HasValue && index >= sheet.FrameCount.Value)
                break;

            int x = sheet.StartX + c * (sheet.FrameWidth + sheet.SpacingX);
            int y = sheet.StartY + r * (sheet.FrameHeight + sheet.SpacingY);

            string spriteId = BuildId(sheet.IdPattern, sheet.Prefix, index, r, c);

            atlas.Sprites[spriteId] = new SpriteV2
            {
                TextureKey = sheet.TextureKey,
                SourceRect = new[] { x, y, sheet.FrameWidth, sheet.FrameHeight },
                PixelsPerUnit = sheet.PixelsPerUnit,
                OriginPixels = origin,
                DefaultOriginToCenter = sheet.DefaultOriginToCenter
            };

            if (clipDto != null)
            {
                if (index >= clipStart && clipFrameCounter < clipMaxCount)
                {
                    clipDto.Frames.Add(new AnimationFrameDto
                    {
                        SpriteId = spriteId,
                        DurationSeconds = frameDuration
                    });
                    clipFrameCounter++;
                }
            }
        }
    }

    if (clipDto != null && clipDto.Frames.Count == 0)
        throw new Exception($"Clip '{sheet.Clip!.ClipId}' generated 0 frames. Check startIndex/frameCount/grid.");
}



Directory.CreateDirectory(Path.GetDirectoryName(atlasOutPath)!);

var outOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

File.WriteAllText(atlasOutPath, JsonSerializer.Serialize(atlas, outOpts));
Console.WriteLine($"Wrote atlas: {atlasOutPath}");
Console.WriteLine($"Sprites: {atlas.Sprites.Count}");

if (anims != null && animOutPath != null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(animOutPath)!);
    File.WriteAllText(animOutPath, JsonSerializer.Serialize(anims, outOpts));
    Console.WriteLine($"Wrote animations: {animOutPath}");
    Console.WriteLine($"Clips: {anims.Clips.Count}");
}

// -------- helpers + DTOs --------

static void Fail(string msg)
{
    Console.WriteLine(msg);
    Environment.ExitCode = 5;
    throw new Exception(msg);
}

static string BuildId(string? pattern, string prefix, int index, int row, int col)
{
    string p = string.IsNullOrWhiteSpace(pattern) ? "{prefix}_{index}" : pattern;

    return p
        .Replace("{prefix}", prefix)
        .Replace("{index}", index.ToString())
        .Replace("{row}", row.ToString())
        .Replace("{col}", col.ToString())
        .Replace("{i2}", index.ToString("D2"))
        .Replace("{i3}", index.ToString("D3"))
        .Replace("{i4}", index.ToString("D4"));
}

public sealed class SliceSpec
{
    [JsonPropertyName("sheets")]
    public List<SpriteSheetSpec> Sheets { get; set; } = new();
}

public sealed class SpriteSheetSpec
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "Sheet";

    [JsonPropertyName("textureKey")]
    public string TextureKey { get; set; } = "";

    [JsonPropertyName("prefix")]
    public string Prefix { get; set; } = "sprite";

    [JsonPropertyName("idPattern")]
    public string? IdPattern { get; set; } = "{prefix}_{i2}";

    [JsonPropertyName("startX")]
    public int StartX { get; set; } = 0;

    [JsonPropertyName("startY")]
    public int StartY { get; set; } = 0;

    [JsonPropertyName("frameWidth")]
    public int FrameWidth { get; set; } = 64;

    [JsonPropertyName("frameHeight")]
    public int FrameHeight { get; set; } = 64;

    [JsonPropertyName("columns")]
    public int Columns { get; set; } = 1;

    [JsonPropertyName("rows")]
    public int Rows { get; set; } = 1;

    [JsonPropertyName("frameCount")]
    public int? FrameCount { get; set; } = null;

    [JsonPropertyName("spacingX")]
    public int SpacingX { get; set; } = 0;

    [JsonPropertyName("spacingY")]
    public int SpacingY { get; set; } = 0;

    [JsonPropertyName("pixelsPerUnit")]
    public float PixelsPerUnit { get; set; } = 100f;

    [JsonPropertyName("originPixels")]
    public float[]? OriginPixels { get; set; } = null;

    [JsonPropertyName("defaultOriginToCenter")]
    public bool DefaultOriginToCenter { get; set; } = true;

    // THIS is the key part:
    [JsonPropertyName("clip")]
    public ClipSpec? Clip { get; set; } = null;
}

public sealed class ClipSpec
{
    [JsonPropertyName("clipId")]
    public string ClipId { get; set; } = "";

    [JsonPropertyName("loop")]
    public bool Loop { get; set; } = true;

    [JsonPropertyName("fps")]
    public float? Fps { get; set; } = null;

    [JsonPropertyName("durationSeconds")]
    public float? DurationSeconds { get; set; } = null;

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; } = 0;

    [JsonPropertyName("frameCount")]
    public int? FrameCount { get; set; } = null;
}

public sealed class AtlasV2
{
    public int Version { get; set; } = 2;
    public Dictionary<string, SpriteV2> Sprites { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class SpriteV2
{
    public string TextureKey { get; set; } = "";
    public int[] SourceRect { get; set; } = new[] { 0, 0, 0, 0 };
    public float PixelsPerUnit { get; set; } = 100f;
    public float[]? OriginPixels { get; set; } = null;
    public bool DefaultOriginToCenter { get; set; } = true;
}

public sealed class AnimationsFile
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("clips")]
    public Dictionary<string, AnimationClipDto> Clips { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AnimationClipDto
{
    [JsonPropertyName("loop")]
    public bool Loop { get; set; } = true;

    [JsonPropertyName("frames")]
    public List<AnimationFrameDto> Frames { get; set; } = new();
}

public sealed class AnimationFrameDto
{
    [JsonPropertyName("spriteId")]
    public string SpriteId { get; set; } = "";

    [JsonPropertyName("durationSeconds")]
    public float DurationSeconds { get; set; } = 0.1f;
}



