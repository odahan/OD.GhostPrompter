using CommunityToolkit.Mvvm.ComponentModel;
using GhostPrompter.Models;
using System.Windows.Media;

namespace GhostPrompter.ViewModels;

/// <summary>Exposes the render-ready presentation state for the no-chrome prompter window.</summary>
public sealed partial class PrompterViewModel : ObservableObject
{
    [ObservableProperty] private string _content = "GhostPrompter is ready.\nLoad a TXT, Markdown or DOCX script.";
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private double _backgroundOpacity = 0.75;
    [ObservableProperty] private double _textOpacity = 0.75;
    [ObservableProperty] private double _fontSize = 30;
    [ObservableProperty] private bool _showProgress = true;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private double _scrollOffset;
    [ObservableProperty] private bool _isScrollMode;
    [ObservableProperty] private double _startingHeight = 50;
    [ObservableProperty] private IReadOnlyList<PrompterElement> _renderElements =
    [
        new(PrompterElementKind.Text, "GhostPrompter is ready.", 0),
        new(PrompterElementKind.Text, "Load a TXT, Markdown or DOCX script.", 0),
    ];
    public Brush BackgroundBrush => new SolidColorBrush(Colors.Black) { Opacity = BackgroundOpacity };
    public void RefreshBackgroundBrush() => OnPropertyChanged(nameof(BackgroundBrush));
}
