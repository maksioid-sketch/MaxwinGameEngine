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

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;
        ViewModel.Initialize(AppContext.BaseDirectory);
        DetailsTree.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(DetailsTree_OnExpanded));
        DetailsTree.AddHandler(TreeViewItem.CollapsedEvent, new RoutedEventHandler(DetailsTree_OnCollapsed));
        Loaded += (_, _) => TryStartGameHost();
        Closed += (_, _) => GameHost.StopGame();
    }

    private void TryStartGameHost()
    {
        var exePath = FindSandboxGameExe();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            ViewModel.StatusText = "SandboxGame executable not found. Build SandboxGame first.";
            return;
        }

        GameHost.StartGame(exePath, Path.GetDirectoryName(exePath), "--editor", restart: true);
        ViewModel.StatusText = "Game started in Edit mode.";
    }

    private void PlayButton_OnClick(object sender, RoutedEventArgs e)
    {
        var exePath = FindSandboxGameExe();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            ViewModel.StatusText = "SandboxGame executable not found. Build SandboxGame first.";
            return;
        }

        GameHost.StartGame(exePath, Path.GetDirectoryName(exePath), "--play", restart: true);
        ViewModel.StatusText = "Game started in Play mode.";
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        var exePath = FindSandboxGameExe();
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            ViewModel.StatusText = "SandboxGame executable not found. Build SandboxGame first.";
            return;
        }

        GameHost.StartGame(exePath, Path.GetDirectoryName(exePath), "--editor", restart: true);
        ViewModel.StatusText = "Game stopped (Edit mode).";
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
