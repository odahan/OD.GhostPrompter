using System.IO;
using System.Windows;
using System.Windows.Input;
using GhostPrompter.Services;
using GhostPrompter.ViewModels;
using Microsoft.Win32;

namespace GhostPrompter;

/// <summary>Provides a focused dark editor that saves corrected scripts as TXT files.</summary>
public partial class TextEditorWindow : Window
{
    private readonly TextFileSaveService _saveService = new();
    private readonly string _suggestedFileName;

    public TextEditorWindow(string text, string sourcePath)
    {
        InitializeComponent();
        ViewModel = new TextEditorViewModel(text);
        DataContext = ViewModel;
        _suggestedFileName = CreateSuggestedFileName(sourcePath);
        Loaded += (_, _) => Editor.Focus();
    }

    public TextEditorViewModel ViewModel { get; }
    public string? SavedPath { get; private set; }

    private async void OnSaveAsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save edited prompter text",
            Filter = "Text files (*.txt)|*.txt",
            DefaultExt = ".txt",
            AddExtension = true,
            FileName = _suggestedFileName,
            OverwritePrompt = true,
            ValidateNames = true,
        };
        if (dialog.ShowDialog(this) != true) return;
        if (!Path.GetExtension(dialog.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            ViewModel.HasError = true;
            ViewModel.Message = "Choose a file name with the .txt extension.";
            return;
        }

        try
        {
            await _saveService.SaveAsync(dialog.FileName, ViewModel.Text);
            SavedPath = dialog.FileName;
            DialogResult = true;
        }
        catch (Exception exception)
        {
            ViewModel.HasError = true;
            ViewModel.Message = $"Could not save the text: {exception.Message}";
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private static string CreateSuggestedFileName(string sourcePath)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        return string.IsNullOrWhiteSpace(baseName) ? "prompter-edited.txt" : $"{baseName}-edited.txt";
    }
}
