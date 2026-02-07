using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using EditorWpf.ViewModels;
using Microsoft.Win32;

namespace EditorWpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }
    private bool _isPlayMode;
    private bool _isMouseOverGameHost;
    private System.Diagnostics.Process? _playProcess;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;
        ViewModel.Initialize(AppContext.BaseDirectory);
        DetailsTree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(DetailsTree_OnExpanded));
        DetailsTree.AddHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(DetailsTree_OnCollapsed));
        Loaded += (_, _) => TryStartGameHost();
        Closed += (_, _) =>
        {
            StopPlayProcess();
            GameHost.StopGame();
        };
    }

    private void TryStartGameHost()
    {
        _isPlayMode = false;
        ViewModel.AutoSaveEnabled = true;
        UpdatePlayToggleButton();

        var exePath = FindSandboxGameExe();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            ViewModel.StatusText = "SandboxGame executable not found. Build SandboxGame first.";
            return;
        }

        GameHost.StartGame(exePath, Path.GetDirectoryName(exePath), "--editor", restart: true);
        UpdateGameHostInputState();
        ViewModel.StatusText = "Game started in Edit mode.";
    }

    private bool StartPlayProcess()
    {
        var exePath = FindSandboxGameExe();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            ViewModel.StatusText = "SandboxGame executable not found. Build SandboxGame first.";
            return false;
        }

        if (_playProcess is not null && !_playProcess.HasExited)
            return true;

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = exePath,
            WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
            UseShellExecute = true,
            Arguments = "--play"
        };

        _playProcess = System.Diagnostics.Process.Start(startInfo);
        if (_playProcess is null)
            return false;

        _playProcess.EnableRaisingEvents = true;
        _playProcess.Exited += (_, _) =>
        {
            Dispatcher.Invoke(() =>
            {
                _isPlayMode = false;
                ViewModel.AutoSaveEnabled = true;
                UpdatePlayToggleButton();
                UpdateGameHostInputState();
                ViewModel.StatusText = "Play window closed.";
            });
        };
        return true;
    }

    private void StopPlayProcess()
    {
        if (_playProcess is null)
            return;

        try
        {
            if (!_playProcess.HasExited)
            {
                _playProcess.CloseMainWindow();
                if (!_playProcess.WaitForExit(1500))
                    _playProcess.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort
        }
        finally
        {
            _playProcess.Dispose();
            _playProcess = null;
        }
    }

    private void PlayToggleButton_OnClick(object sender, RoutedEventArgs e)
    {
        var exePath = FindSandboxGameExe();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            ViewModel.StatusText = "SandboxGame executable not found. Build SandboxGame first.";
            return;
        }

        if (_isPlayMode)
        {
            StopPlayProcess();
            _isPlayMode = false;
            ViewModel.AutoSaveEnabled = true;
            UpdatePlayToggleButton();
            UpdateGameHostInputState();
            ViewModel.StatusText = "Play window closed (Edit mode).";
            return;
        }

        if (!StartPlayProcess())
            return;
        _isPlayMode = true;
        ViewModel.AutoSaveEnabled = false;
        UpdatePlayToggleButton();
        UpdateGameHostInputState();
        ViewModel.StatusText = "Game started in Play window.";
    }

    private void GameHost_OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isMouseOverGameHost = true;
        UpdateGameHostInputState();
        if (_isPlayMode)
        {
            GameHost.Focus();
            GameHost.FocusGameWindow();
        }
    }

    private void GameHost_OnPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        UpdateGameHostInputState();
        GameHost.Focus();
        GameHost.FocusGameWindow();
        e.Handled = true;
    }

    private void GameHost_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isMouseOverGameHost = false;
        UpdateGameHostInputState();
    }

    private void UpdateGameHostInputState()
    {
        if (GameHost is null)
            return;

        var enable = _isPlayMode || _isMouseOverGameHost;
        GameHost.SetInputEnabled(enable);
    }

    private void UpdatePlayToggleButton()
    {
        if (PlayToggleButton is null)
            return;

        var icon = _isPlayMode ? "■" : "▶";
        var color = _isPlayMode ? "#E25555" : "#3BD671";
        PlayToggleButton.ToolTip = _isPlayMode ? "Stop" : "Play";
        PlayToggleButton.Content = new System.Windows.Controls.TextBlock
        {
            Text = icon,
            FontSize = 14,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"),
            Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
    }

    private static string? FindSandboxGameExe()
    {
        var root = TryFindRepoRoot(AppContext.BaseDirectory);
        if (root is null)
            return null;

        var debugExe = Path.Combine(root, "src", "SandboxGame", "bin", "Debug", "net9.0", "SandboxGame.exe");
        if (File.Exists(debugExe))
            return debugExe;

        var releaseExe = Path.Combine(root, "src", "SandboxGame", "bin", "Release", "net9.0", "SandboxGame.exe");
        if (File.Exists(releaseExe))
            return releaseExe;

        return null;
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
            try
            {
                ViewModel.OpenScene(dlg.FileName);
            }
            catch (Exception ex)
            {
                ViewModel.StatusText = "Load failed: " + ex.Message;
                MessageBox.Show(this, ex.Message, "Load failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.ReloadScene();
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = "Load failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Load failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SaveScene();
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = "Save failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EntitiesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = EntitiesList.SelectedItem as EntityView;
        ViewModel.SelectEntity(selected);
        RestoreExpandedState();
    }

    private void DetailsTree_OnExpanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item)
            return;

        if (item.DataContext is ComponentNode node)
            ViewModel.MarkExpanded(node.Name);
    }

    private void DetailsTree_OnCollapsed(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not TreeViewItem item)
            return;

        if (item.DataContext is ComponentNode node)
            ViewModel.MarkCollapsed(node.Name);
    }

    private void DetailsTree_OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var viewer = FindScrollViewer(DetailsTree);
        if (viewer is null)
            return;

        CloseOpenComboBoxes(DetailsTree);

        var delta = -e.Delta * 0.3;
        var target = Math.Max(0, Math.Min(viewer.ScrollableHeight, viewer.VerticalOffset + delta));
        viewer.ScrollToVerticalOffset(target);
        e.Handled = true;
    }

    private void Window_OnPreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        if (source is not null && IsResetButtonClick(source))
            return;

        CommitFocusedDetailEdit(source);
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
            foreach (var item in ViewModel.Details)
            {
                var container = (TreeViewItem)DetailsTree.ItemContainerGenerator.ContainerFromItem(item);
                if (container is null)
                    continue;

                container.IsExpanded = ViewModel.IsExpanded(item.Name);
            }
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private static void CloseOpenComboBoxes(DependencyObject root)
    {
        if (root is ComboBox combo && combo.IsDropDownOpen)
            combo.IsDropDownOpen = false;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            CloseOpenComboBoxes(child);
        }
    }

    private void CommitFocusedDetailEdit(DependencyObject? clickSource)
    {
        if (System.Windows.Input.Keyboard.FocusedElement is not TextBox box)
            return;

        if (clickSource is not null && IsDescendantOf(box, clickSource))
            return;

        if (box.DataContext is not FieldNode node)
            return;

        var value = node.Kind switch
        {
            Engine.Core.Inspection.FieldKind.Vector2 => MainWindowViewModel.ComposeVectorValue(node),
            Engine.Core.Inspection.FieldKind.Vector3 => MainWindowViewModel.ComposeVectorValue(node),
            Engine.Core.Inspection.FieldKind.Color4 => MainWindowViewModel.ComposeVectorValue(node),
            _ => box.Text ?? string.Empty
        };

        ViewModel.ApplyDetailEdit(node, value);
        System.Windows.Input.Keyboard.ClearFocus();
    }

    private bool IsResetButtonClick(DependencyObject source)
    {
        var button = FindAncestor<Button>(source);
        if (button is null)
            return false;

        if (button.Style is null)
            return false;

        var resetStyle = TryFindResource("ResetIconButtonStyle") as Style;
        return resetStyle is not null && ReferenceEquals(button.Style, resetStyle);
    }

    private static T? FindAncestor<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool IsDescendantOf(DependencyObject ancestor, DependencyObject child)
    {
        var current = child;
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return false;
    }

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
}
