using System.Text.Json;
using System.Text.Json.Serialization;

Console.WriteLine("AtlasSlicer START");
Console.WriteLine("Args: " + string.Join(" | ", args));

if (args.Length < 2)
{
    Console.WriteLine("Usage: Tools.AtlasSlicer <input atlas_slices.json> <output atlas.json>");
    Environment.ExitCode = 2;
    return;
}

var inputPath = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);

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

foreach (var sheet in spec.Sheets)
{
    if (sheet.FrameWidth <= 0 || sheet.FrameHeight <= 0)
        Fail($"Invalid frame size for sheet '{sheet.Name}'");

    if (sheet.Columns <= 0 || sheet.Rows <= 0)
        Fail($"Invalid grid size for sheet '{sheet.Name}'");

    var origin = sheet.OriginPixels ?? (sheet.DefaultOriginToCenter
        ? new float[] { sheet.FrameWidth * 0.5f, sheet.FrameHeight * 0.5f }
        : new float[] { 0f, 0f });

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
        }
    }
}

Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var outOpts = new JsonSerializerOptions
{
    WriteIndented = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

File.WriteAllText(outputPath, JsonSerializer.Serialize(atlas, outOpts));

Console.WriteLine($"Wrote: {outputPath}");
Console.WriteLine($"Sprites: {atlas.Sprites.Count}");
Environment.ExitCode = 0;

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
    public List<SpriteSheetSpec> Sheets { get; set; } = new();
}

public sealed class SpriteSheetSpec
{
    public string Name { get; set; } = "Sheet";
    public string TextureKey { get; set; } = "";

    public string Prefix { get; set; } = "sprite";
    public string? IdPattern { get; set; } = "{prefix}_{i2}";

    public int StartX { get; set; } = 0;
    public int StartY { get; set; } = 0;
    public int FrameWidth { get; set; } = 64;
    public int FrameHeight { get; set; } = 64;
    public int Columns { get; set; } = 1;
    public int Rows { get; set; } = 1;

    public int? FrameCount { get; set; } = null;

    public int SpacingX { get; set; } = 0;
    public int SpacingY { get; set; } = 0;

    public float PixelsPerUnit { get; set; } = 100f;

    public float[]? OriginPixels { get; set; } = null; // [x,y]
    public bool DefaultOriginToCenter { get; set; } = true;
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
