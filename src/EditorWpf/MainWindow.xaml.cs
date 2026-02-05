using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Windows;
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
    private Scene? _scene;
    private string? _currentScenePath;

    public MainWindow()
    {
        InitializeComponent();
        EntitiesList.ItemsSource = _entities;
        InspectorList.ItemsSource = _inspectorLines;

        var root = TryFindRepoRoot(AppContext.BaseDirectory);
        if (root is not null)
        {
            var candidate = Path.Combine(root, "src", "SandboxGame", "Scenes", "test.scene.json");
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

    private void EntitiesList_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var selected = EntitiesList.SelectedItem as EntityView;
        UpdateInspector(selected?.Entity);
    }

    private void LoadScene(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            _scene = SceneJson.Deserialize(json);
            _entities.Clear();
            _inspectorLines.Clear();

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
