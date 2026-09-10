using CommunityToolkit.Mvvm.ComponentModel;

namespace GhostPrompter.ViewModels;

/// <summary>Stores the editable script text and save feedback for the text editor.</summary>
public sealed partial class TextEditorViewModel(string text) : ObservableObject
{
    [ObservableProperty] private string _text = text;
    [ObservableProperty] private string _message = "Save As creates a new UTF-8 text file.";
    [ObservableProperty] private bool _hasError;
}
