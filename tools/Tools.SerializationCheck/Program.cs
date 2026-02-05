using Engine.Core.Serialization;

Console.WriteLine("SerializationCheck START");
Console.WriteLine("Args: " + string.Join(" | ", args));

if (args.Length < 1)
{
    Console.WriteLine("Usage: Tools.SerializationCheck <path> [--rewrite] [--fail-on-change]");
    Environment.ExitCode = 2;
    return;
}

var root = Path.GetFullPath(args[0]);
bool rewrite = args.Contains("--rewrite", StringComparer.OrdinalIgnoreCase);
bool failOnChange = args.Contains("--fail-on-change", StringComparer.OrdinalIgnoreCase) || !rewrite;

if (!File.Exists(root) && !Directory.Exists(root))
{
    Console.WriteLine($"Path not found: {root}");
    Environment.ExitCode = 3;
    return;
}

var files = new List<string>();
if (File.Exists(root))
{
    files.Add(root);
}
else
{
    files.AddRange(Directory.EnumerateFiles(root, "*.scene.json", SearchOption.AllDirectories));
    files.AddRange(Directory.EnumerateFiles(root, "*.prefab.json", SearchOption.AllDirectories));
}

if (files.Count == 0)
{
    Console.WriteLine("No scene or prefab JSON files found.");
    Environment.ExitCode = 0;
    return;
}

int changed = 0;
int failed = 0;

foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
{
    try
    {
        if (!TryNormalize(file, out var normalized, out var reason))
        {
            Console.WriteLine($"Skip: {file} ({reason})");
            continue;
        }

        var original = File.ReadAllText(file);
        if (NormalizeNewlines(original) != NormalizeNewlines(normalized))
        {
            changed++;
            Console.WriteLine($"Changed: {file}");

            if (rewrite)
                File.WriteAllText(file, normalized);
            else if (failOnChange)
                failed++;
        }
        else
        {
            Console.WriteLine($"OK: {file}");
        }
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"Error: {file} ({ex.Message})");
    }
}

Console.WriteLine($"Files: {files.Count}, Changed: {changed}, Errors: {failed}");
if (failed > 0)
    Environment.ExitCode = 4;

static string NormalizeNewlines(string value)
{
    return value.Replace("\r\n", "\n");
}

static bool TryNormalize(string file, out string normalized, out string reason)
{
    normalized = string.Empty;
    reason = string.Empty;

    if (file.EndsWith(".scene.json", StringComparison.OrdinalIgnoreCase))
    {
        var scene = SceneJson.Deserialize(File.ReadAllText(file));
        normalized = SceneJson.Serialize(scene);
        return true;
    }

    if (file.EndsWith(".prefab.json", StringComparison.OrdinalIgnoreCase))
    {
        var prefab = PrefabJson.Deserialize(File.ReadAllText(file));
        normalized = PrefabJson.Serialize(prefab);
        return true;
    }

    reason = "Unsupported file extension";
    return false;
}
