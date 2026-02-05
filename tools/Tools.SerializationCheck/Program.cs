using System.Numerics;
using Engine.Core.Components;
using Engine.Core.Math;
using Engine.Core.Scene;
using Engine.Core.Serialization;

Console.WriteLine("SerializationCheck START");
Console.WriteLine("Args: " + string.Join(" | ", args));

bool selfTest = args.Contains("--self-test", StringComparer.OrdinalIgnoreCase);
if (selfTest)
{
    if (!RunSelfTest())
    {
        Environment.ExitCode = 5;
        return;
    }
}

string? pathArg = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
if (pathArg is null)
{
    if (selfTest)
        return;

    Console.WriteLine("Usage: Tools.SerializationCheck <path> [--rewrite] [--fail-on-change] [--self-test]");
    Environment.ExitCode = 2;
    return;
}

var root = Path.GetFullPath(pathArg);
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

static bool RunSelfTest()
{
    try
    {
        var scene = new Scene();
        var id = Guid.NewGuid();
        var entity = scene.CreateEntity(id, "TestEntity");

        entity.Transform.Position = new Vector3(1.25f, -2.5f, 3.75f);
        entity.Transform.Scale = new Vector3(0.9f, 1.1f, 1.0f);
        entity.Transform.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, DegToRad(30f));

        var srcSprite = new SpriteRenderer
        {
            SpriteId = "test_sprite",
            Layer = 2,
            Tint = new Color4(0.1f, 0.2f, 0.3f, 0.4f)
        };
        entity.Add(srcSprite);

        entity.Add(new Animator
        {
            ControllerId = "test_controller",
            ClipId = "test_clip",
            Playing = false,
            Speed = 1.25f,
            LoopOverride = true,
            Loop = false,
            FrameIndex = 3,
            TimeIntoFrame = 0.75f,
            DefaultCrossFadeSeconds = 0.12f,
            DefaultFreezeDuringCrossFade = true
        });

        entity.Add(new BoxCollider2D
        {
            Size = new Vector2(1.5f, 2.5f),
            Offset = new Vector2(-0.25f, 0.75f),
            IsTrigger = true
        });

        entity.Add(new PhysicsBody2D
        {
            IsStatic = true
        });

        entity.Add(new Rigidbody2D
        {
            Mass = 2.2f,
            Velocity = new Vector2(-4.5f, 6.25f),
            UseGravity = false,
            GravityScale = 2.5f,
            LinearDrag = 0.1f,
            Friction = 0.8f
        });

        entity.Add(new DebugRender2D
        {
            ShowCollider = true
        });

        var json = SceneJson.Serialize(scene);
        var roundTrip = SceneJson.Deserialize(json);

        if (roundTrip.Entities.Count != 1)
            throw new Exception("Self-test: entity count mismatch.");

        var e = roundTrip.Entities[0];
        AssertEqual(id, e.Id, "Entity.Id");
        AssertEqual("TestEntity", e.Name, "Entity.Name");

        AssertVector3(entity.Transform.Position, e.Transform.Position, "Transform.Position");
        AssertVector3(entity.Transform.Scale, e.Transform.Scale, "Transform.Scale");
        AssertApprox(GetZRotationRadians(entity.Transform.Rotation), GetZRotationRadians(e.Transform.Rotation), "Transform.RotationZ");

        if (!e.TryGet<SpriteRenderer>(out var spr) || spr is null)
            throw new Exception("Self-test: SpriteRenderer missing.");
        AssertEqual("test_sprite", spr.SpriteId, "SpriteRenderer.SpriteId");
        AssertEqual(2, spr.Layer, "SpriteRenderer.Layer");
        AssertColor(srcSprite.Tint, spr.Tint, "SpriteRenderer.Tint");

        if (!e.TryGet<Animator>(out var anim) || anim is null)
            throw new Exception("Self-test: Animator missing.");
        AssertEqual("test_controller", anim.ControllerId, "Animator.ControllerId");
        AssertEqual("test_clip", anim.ClipId, "Animator.ClipId");
        AssertEqual(false, anim.Playing, "Animator.Playing");
        AssertApprox(1.25f, anim.Speed, "Animator.Speed");
        AssertEqual(true, anim.LoopOverride, "Animator.LoopOverride");
        AssertEqual(false, anim.Loop, "Animator.Loop");
        AssertEqual(3, anim.FrameIndex, "Animator.FrameIndex");
        AssertApprox(0.75f, anim.TimeIntoFrame, "Animator.TimeIntoFrame");
        AssertApprox(0.12f, anim.DefaultCrossFadeSeconds, "Animator.DefaultCrossFadeSeconds");
        AssertEqual(true, anim.DefaultFreezeDuringCrossFade, "Animator.DefaultFreezeDuringCrossFade");

        if (!e.TryGet<BoxCollider2D>(out var box) || box is null)
            throw new Exception("Self-test: BoxCollider2D missing.");
        AssertVector2(new Vector2(1.5f, 2.5f), box.Size, "BoxCollider2D.Size");
        AssertVector2(new Vector2(-0.25f, 0.75f), box.Offset, "BoxCollider2D.Offset");
        AssertEqual(true, box.IsTrigger, "BoxCollider2D.IsTrigger");

        if (!e.TryGet<PhysicsBody2D>(out var body) || body is null)
            throw new Exception("Self-test: PhysicsBody2D missing.");
        AssertEqual(true, body.IsStatic, "PhysicsBody2D.IsStatic");

        if (!e.TryGet<Rigidbody2D>(out var rb) || rb is null)
            throw new Exception("Self-test: Rigidbody2D missing.");
        AssertApprox(2.2f, rb.Mass, "Rigidbody2D.Mass");
        AssertVector2(new Vector2(-4.5f, 6.25f), rb.Velocity, "Rigidbody2D.Velocity");
        AssertEqual(false, rb.UseGravity, "Rigidbody2D.UseGravity");
        AssertApprox(2.5f, rb.GravityScale, "Rigidbody2D.GravityScale");
        AssertApprox(0.1f, rb.LinearDrag, "Rigidbody2D.LinearDrag");
        AssertApprox(0.8f, rb.Friction, "Rigidbody2D.Friction");

        if (!e.TryGet<DebugRender2D>(out var debug) || debug is null)
            throw new Exception("Self-test: DebugRender2D missing.");
        AssertEqual(true, debug.ShowCollider, "DebugRender2D.ShowCollider");

        Console.WriteLine("Self-test OK.");
        return true;
    }
    catch (Exception ex)
    {
        Console.WriteLine("Self-test FAILED: " + ex.Message);
        return false;
    }
}

static void AssertEqual<T>(T expected, T actual, string label) where T : notnull
{
    if (!Equals(expected, actual))
        throw new Exception($"{label} expected '{expected}', got '{actual}'.");
}

static void AssertApprox(float expected, float actual, string label, float eps = 0.0001f)
{
    if (MathF.Abs(expected - actual) > eps)
        throw new Exception($"{label} expected '{expected}', got '{actual}'.");
}

static void AssertVector2(Vector2 expected, Vector2 actual, string label)
{
    AssertApprox(expected.X, actual.X, $"{label}.X");
    AssertApprox(expected.Y, actual.Y, $"{label}.Y");
}

static void AssertVector3(Vector3 expected, Vector3 actual, string label)
{
    AssertApprox(expected.X, actual.X, $"{label}.X");
    AssertApprox(expected.Y, actual.Y, $"{label}.Y");
    AssertApprox(expected.Z, actual.Z, $"{label}.Z");
}

static void AssertColor(Color4 expected, Color4 actual, string label)
{
    AssertApprox(expected.R, actual.R, $"{label}.R");
    AssertApprox(expected.G, actual.G, $"{label}.G");
    AssertApprox(expected.B, actual.B, $"{label}.B");
    AssertApprox(expected.A, actual.A, $"{label}.A");
}

static float GetZRotationRadians(Quaternion q)
{
    var siny_cosp = 2f * (q.W * q.Z + q.X * q.Y);
    var cosy_cosp = 1f - 2f * (q.Y * q.Y + q.Z * q.Z);
    return (float)Math.Atan2(siny_cosp, cosy_cosp);
}

static float DegToRad(float degrees) => degrees * (MathF.PI / 180f);
