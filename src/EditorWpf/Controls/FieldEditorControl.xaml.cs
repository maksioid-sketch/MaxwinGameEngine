using System;
using System.Windows;
using System.Windows.Controls;
using EditorWpf.ViewModels;

namespace EditorWpf.Controls;

public partial class FieldEditorControl : UserControl
{
    public FieldEditorControl()
    {
        InitializeComponent();
    }

    private static MainWindowViewModel? GetViewModel()
        => Application.Current?.MainWindow?.DataContext as MainWindowViewModel;

    private void DetailsValue_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not FieldNode node)
            return;

        GetViewModel()?.ApplyDetailEdit(node, box.Text ?? string.Empty);
    }

    private void DetailsValue_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
            return;

        if (sender is not TextBox box || box.DataContext is not FieldNode node)
            return;

        GetViewModel()?.ApplyDetailEdit(node, box.Text ?? string.Empty);
    }

    private void DetailsVectorValue_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box || box.DataContext is not FieldNode node)
            return;

        GetViewModel()?.ApplyDetailEdit(node, MainWindowViewModel.ComposeVectorValue(node));
    }

    private void DetailsVectorValue_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter)
            return;

        if (sender is not TextBox box || box.DataContext is not FieldNode node)
            return;

        GetViewModel()?.ApplyDetailEdit(node, MainWindowViewModel.ComposeVectorValue(node));
    }

    private void DetailsEnum_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox box || box.DataContext is not FieldNode node)
            return;

        if (box.SelectedItem is not string value)
            return;

        if (string.Equals(value, node.ValueText, StringComparison.OrdinalIgnoreCase))
            return;

        GetViewModel()?.ApplyDetailEdit(node, value);
    }

    private void DetailsBool_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox box || box.DataContext is not FieldNode node)
            return;

        var val = box.IsChecked == true ? "true" : "false";
        GetViewModel()?.ApplyDetailEdit(node, val);
    }

    private void DetailsReset_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.DataContext is not FieldNode node)
            return;

        GetViewModel()?.ResetFieldOverride(node);
    }
}
