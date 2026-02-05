using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using Engine.Core.Components;
using Engine.Core.Inspection;
using Engine.Core.Math;
using Engine.Core.Scene;
using Engine.Core.Serialization;
using Microsoft.Win32;

namespace EditorWpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ObservableCollection<EntityView> _entities = new();
    private readonly ObservableCollection<string> _inspectorLines = new();
    private readonly ObservableCollection<ComponentNode> _details = new();
    private readonly HashSet<string> _expandedComponents = new(StringComparer.OrdinalIgnoreCase);
    private Scene? _scene;
    private string? _currentScenePath;
    private string? _prefabsPath;
    private Dictionary<string, Prefab> _prefabs = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        EntitiesList.ItemsSource = _entities;
        InspectorList.ItemsSource = _inspectorLines;
        DetailsTree.ItemsSource = _details;

        var root = TryFindRepoRoot(AppContext.BaseDirectory);
        if (root is not null)
        {
            var candidate = Path.Combine(root, "src", "SandboxGame", "Scenes", "test.scene.json");
            _prefabsPath = Path.Combine(root, "src", "SandboxGame", "Assets", "Prefabs");
            _prefabs = LoadPrefabs(_prefabsPath);
            if (File.Exists(candidate))
            {
                _currentScenePath = candidate;
                ScenePathBox.Text = candidate;
                LoadScene(candidate);
            }
        }
    }

    private void OpenButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Scene JSON (*.scene.json)|*.scene.json|All files (*.*)|*.*",
            Title = "Open Scene"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _currentScenePath = dlg.FileName;
            ScenePathBox.Text = dlg.FileName;
            LoadScene(dlg.FileName);
        }
    }

    private void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentScenePath))
        {
            StatusText.Text = "No scene selected.";
            return;
        }

        LoadScene(_currentScenePath);
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentScenePath))
        {
            StatusText.Text = "No scene selected.";
            return;
        }

        if (_scene is null)
        {
            StatusText.Text = "No scene loaded.";
            return;
        }

        try
        {
            var json = SceneJson.Serialize(_scene);
            File.WriteAllText(_currentScenePath, json);
            StatusText.Text = "Scene saved.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Save failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EntitiesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = EntitiesList.SelectedItem as EntityView;
        UpdateInspector(selected?.Entity);
        UpdateDetails(selected?.Entity);
    }

    private void LoadScene(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            _scene = SceneJson.Deserialize(json);
            _entities.Clear();
            _inspectorLines.Clear();
            _details.Clear();

            foreach (var e in _scene.Entities)
            {
                _entities.Add(new EntityView(e));
            }

            StatusText.Text = $"Loaded: {path}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Load failed: {ex.Message}";
            MessageBox.Show(this, ex.Message, "Load failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
        {
            BuildDetailsTree(orderedTypes);
            RestoreExpandedState();
        }

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

                var prefabValue = prefabInstanceObj is null ? "" : FormatValue(field.Getter?.Invoke(prefabInstanceObj));
                var sceneValue = sceneInstance is null ? "" : FormatValue(field.Getter?.Invoke(sceneInstance));
                var defaultValue = FormatValue(field.DefaultValue);

                bool isOverridden = hasPrefab && sceneInstance is not null && prefabValue != sceneValue;
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
            }
        }
    }

    private static object? TryGetComponent(Entity entity, Type type)
    {
        if (type == typeof(Transform))
            return entity.Transform;

        if (!typeof(IComponent).IsAssignableFrom(type))
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

    private void DetailsValue_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not FieldNode node)
            return;

        ApplyDetailEdit(node, box.Text ?? string.Empty);
    }

    private void DetailsValue_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
            return;

        if (sender is not TextBox box || box.DataContext is not FieldNode node)
            return;

        ApplyDetailEdit(node, box.Text ?? string.Empty);
    }

    private void DetailsBool_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || box.DataContext is not FieldNode node)
            return;

        var val = box.IsChecked == true ? "true" : "false";
        ApplyDetailEdit(node, val);
    }

    private void ApplyDetailEdit(FieldNode node, string valueText)
    {
        if (node.FieldDescriptor is null || node.ComponentType is null)
            return;

        var selected = EntitiesList.SelectedItem as EntityView;
        if (selected?.Entity is null)
            return;

        try
        {
            ApplyOverrideValue(selected.Entity, node.ComponentType, node.FieldDescriptor, valueText);
            UpdateDetails(selected.Entity);
            UpdateInspector(selected.Entity);
            StatusText.Text = "Scene override updated.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Update failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DetailsReset_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not FieldNode node)
            return;

        var selected = EntitiesList.SelectedItem as EntityView;
        if (selected?.Entity is null || node.ComponentType is null || node.FieldDescriptor is null)
            return;

        try
        {
            ResetFieldOverride(selected.Entity, node.ComponentType, node.FieldDescriptor);
            UpdateDetails(selected.Entity);
            UpdateInspector(selected.Entity);
            StatusText.Text = "Field reset.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Reset failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Reset failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

        if (prefabRoot is not null)
        {
            var prefabComponent = GetPrefabComponent(prefabRoot, componentType);
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
                TryClearOverrideIfMatchesPrefab(entity, componentType, prefabComponent, target);
                return;
            }

            // Prefab has no such component: treat reset as "remove override"
            ClearOverrideAndRemoveComponent(entity, componentType);
            return;
        }

        field.Setter?.Invoke(target, field.DefaultValue);
        TryClearOverrideIfMatchesPrefab(entity, componentType, null, target);
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

    private void TryClearOverrideIfMatchesPrefab(Entity entity, Type componentType, object? prefabComponent, object target)
    {
        if (!entity.TryGet<PrefabInstance>(out var pi) || pi is null)
            return;

        Prefab.PrefabEntity? prefabRoot = null;
        if (prefabComponent is null)
        {
            if (!TryGetPrefabRoot(entity, out var root))
                return;
            prefabRoot = root;
        }

        prefabComponent ??= GetPrefabComponent(prefabRoot!, componentType);
        if (prefabComponent is null)
        {
            ClearOverrideAndRemoveComponent(entity, componentType);
            return;
        }

        if (!AreComponentFieldsEqual(prefabComponent, target, componentType))
            return;

        if (componentType == typeof(Transform))
        {
            pi.OverrideTransform = false;
            pi.UsePrefabTransform = true;
        }
        else if (componentType == typeof(SpriteRenderer)) pi.OverrideSpriteRenderer = false;
        else if (componentType == typeof(Animator)) pi.OverrideAnimator = false;
        else if (componentType == typeof(BoxCollider2D)) pi.OverrideBoxCollider2D = false;
        else if (componentType == typeof(PhysicsBody2D)) pi.OverridePhysicsBody2D = false;
        else if (componentType == typeof(Rigidbody2D)) pi.OverrideRigidbody2D = false;
        else if (componentType == typeof(DebugRender2D)) pi.OverrideDebugRender2D = false;
    }

    private void ClearOverrideAndRemoveComponent(Entity entity, Type componentType)
    {
        if (entity.TryGet<PrefabInstance>(out var pi) && pi is not null)
        {
            if (componentType == typeof(Transform))
            {
                pi.OverrideTransform = false;
                pi.UsePrefabTransform = true;
            }
            else if (componentType == typeof(SpriteRenderer)) pi.OverrideSpriteRenderer = false;
            else if (componentType == typeof(Animator)) pi.OverrideAnimator = false;
            else if (componentType == typeof(BoxCollider2D)) pi.OverrideBoxCollider2D = false;
            else if (componentType == typeof(PhysicsBody2D)) pi.OverridePhysicsBody2D = false;
            else if (componentType == typeof(Rigidbody2D)) pi.OverrideRigidbody2D = false;
            else if (componentType == typeof(DebugRender2D)) pi.OverrideDebugRender2D = false;
        }

        if (componentType != typeof(Transform))
            entity.Remove(componentType);
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

    private void DetailsTree_OnExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item)
            return;

        if (item.DataContext is ComponentNode node)
            _expandedComponents.Add(node.Name);
    }

    private void DetailsTree_OnCollapsed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item)
            return;

        if (item.DataContext is ComponentNode node)
            _expandedComponents.Remove(node.Name);
    }

    private void DetailsTree_OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var viewer = FindScrollViewer(DetailsTree);
        if (viewer is null)
            return;

        var delta = -e.Delta * 0.3;
        var target = Math.Max(0, Math.Min(viewer.ScrollableHeight, viewer.VerticalOffset + delta));
        viewer.ScrollToVerticalOffset(target);
        e.Handled = true;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject root)
    {
        if (root is ScrollViewer sv)
            return sv;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            var found = FindScrollViewer(child);
            if (found is not null)
                return found;
        }

        return null;
    }

    private void RestoreExpandedState()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var item in _details)
            {
                var container = (TreeViewItem)DetailsTree.ItemContainerGenerator.ContainerFromItem(item);
                if (container is null)
                    continue;

                container.IsExpanded = _expandedComponents.Contains(item.Name);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
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
            compNode.Fields.Add(new FieldNode(field.Name, "", componentType, field, false, field.Kind == FieldKind.Bool, false));
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

    private sealed class EntityView
    {
        public Entity Entity { get; }
        public string DisplayName { get; }

        public EntityView(Entity entity)
        {
            Entity = entity;
            DisplayName = $"{entity.Name} ({entity.Id.ToString("N")[..8]})";
        }
    }

}
