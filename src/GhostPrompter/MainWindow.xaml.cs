using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using GhostPrompter.ViewModels;

namespace GhostPrompter;

/// <summary>Hosts the dark configuration interface and its native file-selection dialog.</summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public async void OnLoadRequested(object? sender, EventArgs args)
    {
        var dialog = new OpenFileDialog { Filter = "Script files|*.txt;*.md;*.markdown;*.docx|All files|*.*", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) == true && DataContext is MainViewModel viewModel) await viewModel.LoadPathAsync(dialog.FileName);
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is not null) return;
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private static T? FindAncestor<T>(DependencyObject source) where T : DependencyObject
    {
        for (DependencyObject? current = source; current is not null;
             current = current is System.Windows.Media.Visual ? System.Windows.Media.VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (current is T match) return match;
        }
        return null;
    }
}
