using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using Engine.Core.Components;
using Engine.Core.Inspection;
using Engine.Core.Math;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using EngineComponent = Engine.Core.Components.IComponent;

namespace EditorWpf.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly ObservableCollection<EntityView> _entities = new();
    private readonly ObservableCollection<string> _inspectorLines = new();
    private readonly ObservableCollection<ComponentNode> _details = new();
    private readonly HashSet<string> _expandedComponents = new(StringComparer.OrdinalIgnoreCase);
    private Scene? _scene;
    private string? _currentScenePath;
    private string? _prefabsPath;
    private Dictionary<string, Prefab> _prefabs = new(StringComparer.OrdinalIgnoreCase);
    private string _scenePath = string.Empty;
    private string _statusText = "Ready.";
    private Entity? _selectedEntity;
    private bool _autoSaveEnabled = true;

    public ObservableCollection<EntityView> Entities => _entities;
    public ObservableCollection<string> InspectorLines => _inspectorLines;
    public ObservableCollection<ComponentNode> Details => _details;

    public string ScenePath
    {
        get => _scenePath;
        set { if (_scenePath != value) { _scenePath = value; Notify(); } }
    }

    public string StatusText
    {
        get => _statusText;
        set { if (_statusText != value) { _statusText = value; Notify(); } }
    }

    public bool AutoSaveEnabled
    {
        get => _autoSaveEnabled;
        set { if (_autoSaveEnabled != value) { _autoSaveEnabled = value; Notify(); } }
    }

    public void Initialize(string baseDirectory)
    {
        var root = TryFindRepoRoot(baseDirectory);
        if (root is null)
            return;

        var candidate = Path.Combine(root, "src", "SandboxGame", "Scenes", "test.scene.json");
        _prefabsPath = Path.Combine(root, "src", "SandboxGame", "Assets", "Prefabs");
        _prefabs = LoadPrefabs(_prefabsPath);
        if (File.Exists(candidate))
        {
            try
            {
                OpenScene(candidate);
            }
            catch (Exception ex)
            {
                StatusText = $"Load failed: {ex.Message}";
            }
        }
    }

    public void OpenScene(string path)
    {
        _currentScenePath = path;
        ScenePath = path;
        LoadScene(path);
    }

    public void LoadScene(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            _scene = SceneJson.Deserialize(json);
            _entities.Clear();
            _inspectorLines.Clear();
            _details.Clear();

            foreach (var e in _scene.Entities)
                _entities.Add(new EntityView(e));

            StatusText = $"Loaded: {path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Load failed: {ex.Message}";
            throw;
        }
    }

    public void ReloadScene()
    {
        if (string.IsNullOrWhiteSpace(_currentScenePath))
        {
            StatusText = "No scene selected.";
            return;
        }

        LoadScene(_currentScenePath);
    }

    public void SaveScene()
    {
        if (string.IsNullOrWhiteSpace(_currentScenePath))
        {
            StatusText = "No scene selected.";
            return;
        }

        if (_scene is null)
        {
            StatusText = "No scene loaded.";
            return;
        }

        var json = SceneJson.Serialize(_scene);
        File.WriteAllText(_currentScenePath, json);
        StatusText = "Scene saved.";
    }

    public void SelectEntity(EntityView? selected)
    {
        _selectedEntity = selected?.Entity;
        UpdateInspector(selected?.Entity);
        UpdateDetails(selected?.Entity);
    }

    public void MarkExpanded(string name) => _expandedComponents.Add(name);
    public void MarkCollapsed(string name) => _expandedComponents.Remove(name);
    public bool IsExpanded(string name) => _expandedComponents.Contains(name);

    public void ApplyDetailEdit(FieldNode node, string valueText)
    {
        if (node.FieldDescriptor is null || node.ComponentType is null)
            return;

        var entity = _selectedEntity;
        if (entity is null)
            return;

        ApplyOverrideValue(entity, node.ComponentType, node.FieldDescriptor, valueText);
        UpdateDetails(entity);
        UpdateInspector(entity);
        var autoSaved = AutoSaveScene();
        StatusText = autoSaved ? "Scene override updated (auto-saved)." : "Scene override updated.";
    }

    public void ResetFieldOverride(FieldNode node)
    {
        var entity = _selectedEntity;
        if (entity is null || node.ComponentType is null || node.FieldDescriptor is null)
            return;

        ResetFieldOverride(entity, node.ComponentType, node.FieldDescriptor);
        UpdateDetails(entity);
        UpdateInspector(entity);
        var autoSaved = AutoSaveScene();
        StatusText = autoSaved ? "Field reset (auto-saved)." : "Field reset.";
    }

    private bool AutoSaveScene()
    {
        if (!AutoSaveEnabled)
            return false;

        if (string.IsNullOrWhiteSpace(_currentScenePath) || _scene is null)
            return false;

        try
        {
            var json = SceneJson.Serialize(_scene);
            File.WriteAllText(_currentScenePath, json);
            return true;
        }
        catch (Exception ex)
        {
            StatusText = "Auto-save failed: " + ex.Message;
            return false;
        }
    }

    public static string ComposeVectorValue(FieldNode node)
    {
        static string Sanitize(string value) => value?.Trim() ?? string.Empty;

        return node.Kind switch
        {
            FieldKind.Vector2 => $"({Sanitize(node.XValue)}, {Sanitize(node.YValue)})",
            FieldKind.Vector3 => $"({Sanitize(node.XValue)}, {Sanitize(node.YValue)}, {Sanitize(node.ZValue)})",
            FieldKind.Color4 => $"({Sanitize(node.XValue)}, {Sanitize(node.YValue)}, {Sanitize(node.ZValue)}, {Sanitize(node.WValue)})",
            _ => node.ValueText
        };
    }

    private void UpdateInspector(Entity? entity)
    {
        _inspectorLines.Clear();

        if (entity is null)
        {
            _inspectorLines.Add("No selection.");
            return;
        }

        _inspectorLines.Add($"Inspector: {entity.Name}");

        foreach (var desc in ComponentRegistry.All())
        {
            var instance = TryGetComponent(entity, desc.Type);
            if (instance is null)
                continue;

            _inspectorLines.Add(desc.DisplayName + ":");
            foreach (var field in desc.Fields)
            {
                var value = field.Getter?.Invoke(instance);
                _inspectorLines.Add($"  {field.Name} = {FormatValue(value)}");
                if (_inspectorLines.Count >= 100) break;
            }

            if (_inspectorLines.Count >= 100) break;
        }
    }

    private void UpdateDetails(Entity? entity)
    {
        if (entity is null)
        {
            _details.Clear();
            _details.Add(new ComponentNode("No selection"));
            return;
        }

        Prefab.PrefabEntity? prefabRoot = null;
        PrefabInstance? prefabInstance = null;
        if (entity.TryGet<PrefabInstance>(out var pi) && pi is not null && !string.IsNullOrWhiteSpace(pi.PrefabId))
        {
            prefabInstance = pi;
            if (_prefabs.TryGetValue(pi.PrefabId, out var prefab))
            {
                try { prefabRoot = prefab.GetRootEntity(); }
                catch { prefabRoot = null; }
            }
        }

        bool hasPrefab = prefabRoot is not null;

        var orderedTypes = new[]
        {
            typeof(Transform),
            typeof(SpriteRenderer),
            typeof(BoxCollider2D),
            typeof(DebugRender2D),
            typeof(Animator),
            typeof(PhysicsBody2D),
            typeof(Rigidbody2D)
        };

        if (_details.Count == 0)
            BuildDetailsTree(orderedTypes);

        for (int i = 0; i < orderedTypes.Length; i++)
        {
            var type = orderedTypes[i];
            var desc = ComponentRegistry.TryGet(type);
            if (desc is null)
                continue;

            var prefabInstanceObj = prefabRoot is null ? null : GetPrefabComponent(prefabRoot, type);
            var sceneInstance = TryGetComponent(entity, type);

            ComponentNode compNode;
            if (i < _details.Count)
                compNode = _details[i];
            else
            {
                compNode = new ComponentNode(desc.DisplayName);
                _details.Add(compNode);
            }

            if (!string.Equals(compNode.Name, desc.DisplayName, StringComparison.OrdinalIgnoreCase))
                compNode = ReplaceComponentNode(i, desc.DisplayName);

            EnsureFieldNodes(compNode, desc, type);

            for (int f = 0; f < desc.Fields.Count; f++)
            {
                var field = desc.Fields[f];
                var node = compNode.Fields[f];
                node.ComponentInstance = entity;

                var prefabRaw = prefabInstanceObj is null ? null : field.Getter?.Invoke(prefabInstanceObj);
                var sceneRaw = sceneInstance is null ? null : field.Getter?.Invoke(sceneInstance);
                var prefabValue = prefabRaw is null ? "" : FormatValue(prefabRaw);
                var sceneValue = sceneRaw is null ? "" : FormatValue(sceneRaw);
                var defaultValue = FormatValue(field.DefaultValue);

                bool isOverridden = hasPrefab && sceneInstance is not null && !ValuesEqual(prefabRaw, sceneRaw);
                var displayValue = isOverridden
                    ? sceneValue
                    : (hasPrefab ? (string.IsNullOrWhiteSpace(prefabValue) ? defaultValue : prefabValue) : (string.IsNullOrWhiteSpace(sceneValue) ? defaultValue : sceneValue));

                bool isBool = field.Kind == FieldKind.Bool;
                bool boolValue = false;
                if (isBool)
                    bool.TryParse(displayValue, out boolValue);

                node.ValueText = displayValue;
                node.CanReset = isOverridden;
                node.IsBool = isBool;
                node.BoolValue = boolValue;
                if (field.Kind == FieldKind.Enum)
                    node.EnumValue = displayValue;

                UpdateVectorFields(node, field.Kind, displayValue);
            }
        }
    }

    private static object? TryGetComponent(Entity entity, Type type)
    {
        if (type == typeof(Transform))
            return entity.Transform;

        if (!typeof(EngineComponent).IsAssignableFrom(type))
            return null;

        var method = typeof(Entity).GetMethod("TryGet")!;
        var generic = method.MakeGenericMethod(type);
        var args = new object?[] { null };
        var ok = (bool)generic.Invoke(entity, args)!;
        return ok ? args[0] : null;
    }

    private static object? GetPrefabComponent(Prefab.PrefabEntity root, Type type)
    {
        if (type == typeof(Transform))
        {
            var t = new Transform
            {
                Position = root.Position,
                Scale = root.Scale,
                Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, root.RotationZRadians)
            };
            return t;
        }

        if (type == typeof(SpriteRenderer) && root.SpriteRenderer is not null)
            return root.SpriteRenderer.ToComponent();
        if (type == typeof(Animator) && root.Animator is not null)
            return root.Animator.ToComponent();
        if (type == typeof(BoxCollider2D) && root.BoxCollider2D is not null)
            return root.BoxCollider2D.ToComponent();
        if (type == typeof(PhysicsBody2D) && root.PhysicsBody2D is not null)
            return root.PhysicsBody2D.ToComponent();
        if (type == typeof(Rigidbody2D) && root.Rigidbody2D is not null)
            return root.Rigidbody2D.ToComponent();
        if (type == typeof(DebugRender2D) && root.DebugRender2D is not null)
            return root.DebugRender2D.ToComponent();

        return null;
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return "null";

        return value switch
        {
            Vector2 v2 => $"({Fmt(v2.X)}, {Fmt(v2.Y)})",
            Vector3 v3 => $"({Fmt(v3.X)}, {Fmt(v3.Y)}, {Fmt(v3.Z)})",
            Color4 c => $"({Fmt(c.R)}, {Fmt(c.G)}, {Fmt(c.B)}, {Fmt(c.A)})",
            bool b => b ? "true" : "false",
            float f => Fmt(f),
            double d => Fmt(d),
            _ => value.ToString() ?? "null"
        };
    }

    private static void UpdateVectorFields(FieldNode node, FieldKind kind, string valueText)
    {
        float[]? parts = null;
        try
        {
            parts = kind switch
            {
                FieldKind.Vector2 => SplitNumbers(valueText, 2),
                FieldKind.Vector3 => SplitNumbers(valueText, 3),
                FieldKind.Color4 => SplitNumbers(valueText, 4),
                _ => null
            };
        }
        catch
        {
            parts = null;
        }

        if (parts is null)
            return;

        node.XValue = Fmt(parts[0]);
        node.YValue = Fmt(parts[1]);
        if (parts.Length > 2)
            node.ZValue = Fmt(parts[2]);
        if (parts.Length > 3)
            node.WValue = Fmt(parts[3]);
    }

    private static string Fmt(float value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Fmt(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string? TryFindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(startDir);
        while (dir is not null)
        {
            var slnx = Path.Combine(dir.FullName, "MaxwinGameEngine.slnx");
            if (File.Exists(slnx))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    private static Dictionary<string, Prefab> LoadPrefabs(string? prefabsPath)
    {
        var prefabs = new Dictionary<string, Prefab>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(prefabsPath) || !Directory.Exists(prefabsPath))
            return prefabs;

        var files = Directory.GetFiles(prefabsPath, "*.prefab.json", SearchOption.AllDirectories);
        for (int i = 0; i < files.Length; i++)
        {
            var path = files[i];
            var json = File.ReadAllText(path);
            var prefab = PrefabJson.Deserialize(json);

            var name = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path));
            if (string.IsNullOrWhiteSpace(name))
                continue;

            prefabs[name] = prefab;
        }

        return prefabs;
    }

    private void ApplyOverrideValue(Entity entity, Type componentType, FieldDescriptor field, string valueText)
    {
        object? target = componentType == typeof(Transform)
            ? entity.Transform
            : TryGetComponent(entity, componentType);

        Prefab.PrefabEntity? prefabRoot = null;
        if (TryGetPrefabRoot(entity, out var root))
            prefabRoot = root;

        PrefabInstance? pi = null;
        if (entity.TryGet<PrefabInstance>(out var piFound))
            pi = piFound;

        object? prefabComponent = null;
        if (prefabRoot is not null)
        {
            prefabComponent = GetPrefabComponent(prefabRoot, componentType);
            if (prefabComponent is not null)
            {
                if (target is null)
                {
                    target = prefabComponent;
                    var addMethod = typeof(Entity).GetMethod("Add")!.MakeGenericMethod(componentType);
                    addMethod.Invoke(entity, new[] { target });
                }
                else if (pi is not null && !IsOverrideActive(pi, componentType))
                {
                    CopyComponentValues(prefabComponent, target, componentType);
                }
            }
        }

        if (target is null)
        {
            target = Activator.CreateInstance(componentType)
                     ?? throw new InvalidOperationException($"Cannot create component {componentType.Name}.");

            var addMethod = typeof(Entity).GetMethod("Add")!.MakeGenericMethod(componentType);
            addMethod.Invoke(entity, new[] { target });
        }

        var value = ParseFieldValue(field.Kind, field.EnumType, valueText);
        field.Setter?.Invoke(target, value);

        EnsurePrefabOverrideFlag(entity, componentType);
    }

    private static void CopyComponentValues(object source, object target, Type componentType)
    {
        var desc = ComponentRegistry.TryGet(componentType);
        if (desc is null)
            return;

        for (int i = 0; i < desc.Fields.Count; i++)
        {
            var f = desc.Fields[i];
            if (f.Getter is null || f.Setter is null)
                continue;

            var v = f.Getter(source);
            f.Setter(target, v);
        }
    }

    private void ResetFieldOverride(Entity entity, Type componentType, FieldDescriptor field)
    {
        object? target = componentType == typeof(Transform)
            ? entity.Transform
            : TryGetComponent(entity, componentType);

        if (target is null)
        {
            target = Activator.CreateInstance(componentType)
                     ?? throw new InvalidOperationException($"Cannot create component {componentType.Name}.");

            var addMethod = typeof(Entity).GetMethod("Add")!.MakeGenericMethod(componentType);
            addMethod.Invoke(entity, new[] { target });
        }

        if (TryGetPrefabRoot(entity, out var prefabRoot))
        {
            var prefabComponent = GetPrefabComponent(prefabRoot, componentType);
            if (prefabComponent is not null && field.Getter is not null)
            {
                var prefabValue = field.Getter(prefabComponent);
                field.Setter?.Invoke(target, prefabValue);
            }
            else
            {
                field.Setter?.Invoke(target, field.DefaultValue);
            }

            EnsurePrefabOverrideFlag(entity, componentType);
            return;
        }

        field.Setter?.Invoke(target, field.DefaultValue);
        EnsurePrefabOverrideFlag(entity, componentType);
    }

    private static object ParseFieldValue(FieldKind kind, Type? enumType, string text)
    {
        text = text.Trim();

        switch (kind)
        {
            case FieldKind.String:
                return text;
            case FieldKind.Bool:
                return bool.Parse(text);
            case FieldKind.Int:
                return int.Parse(text, CultureInfo.InvariantCulture);
            case FieldKind.Float:
                return float.Parse(text, CultureInfo.InvariantCulture);
            case FieldKind.Vector2:
                return ParseVector2(text);
            case FieldKind.Vector3:
                return ParseVector3(text);
            case FieldKind.Color4:
                return ParseColor4(text);
            case FieldKind.Enum:
                if (enumType is null)
                    throw new InvalidOperationException("Enum type missing.");
                return Enum.Parse(enumType, text, ignoreCase: true);
            default:
                throw new InvalidOperationException("Unsupported field kind.");
        }
    }

    private static Vector2 ParseVector2(string text)
    {
        var parts = SplitNumbers(text, 2);
        return new Vector2(parts[0], parts[1]);
    }

    private static Vector3 ParseVector3(string text)
    {
        var parts = SplitNumbers(text, 3);
        return new Vector3(parts[0], parts[1], parts[2]);
    }

    private static Color4 ParseColor4(string text)
    {
        var parts = SplitNumbers(text, 4);
        return new Color4(parts[0], parts[1], parts[2], parts[3]);
    }

    private static float[] SplitNumbers(string text, int count)
    {
        text = text.Trim().Trim('(', ')');
        var pieces = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (pieces.Length != count)
            throw new FormatException($"Expected {count} values, got {pieces.Length}.");

        var result = new float[count];
        for (int i = 0; i < count; i++)
            result[i] = float.Parse(pieces[i], CultureInfo.InvariantCulture);

        return result;
    }

    private static void EnsurePrefabOverrideFlag(Entity entity, Type componentType)
    {
        if (!entity.TryGet<PrefabInstance>(out var pi) || pi is null)
            return;

        if (componentType == typeof(Transform))
        {
            pi.OverrideTransform = true;
            pi.UsePrefabTransform = false;
        }
        else if (componentType == typeof(SpriteRenderer))
            pi.OverrideSpriteRenderer = true;
        else if (componentType == typeof(Animator))
            pi.OverrideAnimator = true;
        else if (componentType == typeof(BoxCollider2D))
            pi.OverrideBoxCollider2D = true;
        else if (componentType == typeof(PhysicsBody2D))
            pi.OverridePhysicsBody2D = true;
        else if (componentType == typeof(Rigidbody2D))
            pi.OverrideRigidbody2D = true;
        else if (componentType == typeof(DebugRender2D))
            pi.OverrideDebugRender2D = true;
    }


    private static bool AreComponentFieldsEqual(object a, object b, Type componentType)
    {
        var desc = ComponentRegistry.TryGet(componentType);
        if (desc is null)
            return true;

        for (int i = 0; i < desc.Fields.Count; i++)
        {
            var f = desc.Fields[i];
            if (f.Getter is null)
                continue;

            var va = f.Getter(a);
            var vb = f.Getter(b);
            if (!ValuesEqual(va, vb))
                return false;
        }

        return true;
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a is null || b is null)
            return a == b;

        return (a, b) switch
        {
            (float fa, float fb) => MathF.Abs(fa - fb) < 0.0001f,
            (double da, double db) => Math.Abs(da - db) < 0.0001,
            (Vector2 va, Vector2 vb) => Vector2.DistanceSquared(va, vb) < 0.0000001f,
            (Vector3 va, Vector3 vb) => Vector3.DistanceSquared(va, vb) < 0.0000001f,
            (Color4 ca, Color4 cb) =>
                MathF.Abs(ca.R - cb.R) < 0.0001f &&
                MathF.Abs(ca.G - cb.G) < 0.0001f &&
                MathF.Abs(ca.B - cb.B) < 0.0001f &&
                MathF.Abs(ca.A - cb.A) < 0.0001f,
            _ => Equals(a, b)
        };
    }

    private static bool IsOverrideActive(PrefabInstance? pi, Type componentType)
    {
        if (pi is null)
            return false;

        if (componentType == typeof(Transform))
            return pi.OverrideTransform;
        if (componentType == typeof(SpriteRenderer))
            return pi.OverrideSpriteRenderer;
        if (componentType == typeof(Animator))
            return pi.OverrideAnimator;
        if (componentType == typeof(BoxCollider2D))
            return pi.OverrideBoxCollider2D;
        if (componentType == typeof(PhysicsBody2D))
            return pi.OverridePhysicsBody2D;
        if (componentType == typeof(Rigidbody2D))
            return pi.OverrideRigidbody2D;
        if (componentType == typeof(DebugRender2D))
            return pi.OverrideDebugRender2D;

        return false;
    }

    private bool TryGetPrefabRoot(Entity entity, out Prefab.PrefabEntity root)
    {
        root = null!;

        if (!entity.TryGet<PrefabInstance>(out var pi) || pi is null || string.IsNullOrWhiteSpace(pi.PrefabId))
            return false;

        if (!_prefabs.TryGetValue(pi.PrefabId, out var prefab))
            return false;

        try
        {
            root = prefab.GetRootEntity();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void BuildDetailsTree(Type[] orderedTypes)
    {
        _details.Clear();

        foreach (var type in orderedTypes)
        {
            var desc = ComponentRegistry.TryGet(type);
            if (desc is null)
                continue;

            var compNode = new ComponentNode(desc.DisplayName);
            EnsureFieldNodes(compNode, desc, type);
            _details.Add(compNode);
        }
    }

    private void EnsureFieldNodes(ComponentNode compNode, ComponentDescriptor desc, Type componentType)
    {
        if (compNode.Fields.Count == desc.Fields.Count)
            return;

        compNode.Fields.Clear();
        for (int i = 0; i < desc.Fields.Count; i++)
        {
            var field = desc.Fields[i];
            var node = new FieldNode(field.Name, "", componentType, field, false, field.Kind == FieldKind.Bool, false);
            if (field.Kind == FieldKind.Enum && field.EnumType is not null)
            {
                foreach (var name in Enum.GetNames(field.EnumType))
                    node.EnumOptions.Add(name);
            }
            compNode.Fields.Add(node);
        }
    }

    private ComponentNode ReplaceComponentNode(int index, string name)
    {
        var node = new ComponentNode(name);
        if (index >= 0 && index < _details.Count)
            _details[index] = node;
        else
            _details.Add(node);
        return node;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
