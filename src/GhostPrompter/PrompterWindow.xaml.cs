using System.Windows;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Globalization;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Text;
using GhostPrompter.Models;
using GhostPrompter.ViewModels;

namespace GhostPrompter;

/// <summary>Displays the presenter-only transparent overlay.</summary>
public partial class PrompterWindow : Window
{
    private const double ProgressIndicatorHeight = 3;
    private PrompterViewModel? _viewModel;
    public bool IsInteractionLocked { get; set; }
    public event EventHandler? ReflowRequested;
    public double ContentGridWidth => ContentGrid.ActualWidth;
    /// <summary>Gets the usable text height, excluding the optional progress indicator.</summary>
    public double ContentGridHeight => Math.Max(0, ContentGrid.ActualHeight - (_viewModel?.ShowProgress == true ? ProgressIndicatorHeight : 0));

    public PrompterWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (PresentationSource.FromVisual(this) is HwndSource source) source.AddHook(WindowMessageHook);
    }

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        const int wmDpiChanged = 0x02E0;
        if (message == wmDpiChanged) Dispatcher.BeginInvoke(() => ReflowRequested?.Invoke(this, EventArgs.Empty), DispatcherPriority.Render);
        return nint.Zero;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel = e.NewValue as PrompterViewModel;
        if (_viewModel is not null) _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        BuildDocument();
        QueueVisualUpdate();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(PrompterViewModel.Content) or nameof(PrompterViewModel.RenderElements)) BuildDocument();
        if (e.PropertyName is nameof(PrompterViewModel.Content) or nameof(PrompterViewModel.RenderElements) or nameof(PrompterViewModel.FontSize) or nameof(PrompterViewModel.StartingHeight) or nameof(PrompterViewModel.ScrollOffset)) QueueVisualUpdate();
    }

    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueVisualUpdate();
        ReflowRequested?.Invoke(this, EventArgs.Empty);
    }
    private void QueueVisualUpdate() => Dispatcher.BeginInvoke(UpdateVisualLayout, DispatcherPriority.Render);
    private void UpdateVisualLayout()
    {
        if (_viewModel is null || ContentGridHeight <= 0) return;
        var anchor = GetScrollAnchor();
        ContentTransform.Y = anchor - _viewModel.ScrollOffset;
    }

    private double GetScrollAnchor()
    {
        if (_viewModel is null || ContentGridHeight <= 0) return 0;
        var desiredAnchor = ContentGridHeight * _viewModel.StartingHeight / 100;
        var lineHeight = Math.Max(_viewModel.FontSize * 1.2, 1);
        return Math.Min(desiredAnchor, Math.Max(0, ContentGridHeight - lineHeight));
    }

    private void BuildDocument()
    {
        if (_viewModel is null) return;
        var document = new FlowDocument { PagePadding = new Thickness(0), FontFamily = new FontFamily("Segoe UI"), FontSize = _viewModel.FontSize, Foreground = Brushes.White };
        var paragraph = new Paragraph { Margin = new Thickness(0), Padding = new Thickness(0) };
        var text = new StringBuilder();
        PrompterElementKind? activeKind = null;
        var hasPreviousElement = false;

        void FlushRun()
        {
            if (text.Length == 0 || activeKind is null) return;
            var run = new Run(text.ToString());
            if (activeKind == PrompterElementKind.Title) run.FontWeight = FontWeights.Bold;
            if (activeKind == PrompterElementKind.Comment) run.FontStyle = FontStyles.Italic;
            paragraph.Inlines.Add(run);
            text.Clear();
        }

        foreach (var element in _viewModel.RenderElements)
        {
            if (activeKind != element.Kind)
            {
                FlushRun();
                activeKind = element.Kind;
            }
            if (hasPreviousElement) text.Append('\n');
            text.Append(element.Text);
            hasPreviousElement = true;
        }
        FlushRun();
        document.Blocks.Add(paragraph);
        ContentText.Document = document;
    }

    /// <summary>Measures parsed elements with the same WPF text engine used by the overlay.</summary>
    public double MeasureElements(IReadOnlyList<PrompterElement> elements, double width)
    {
        if (_viewModel is null || width <= 0) return double.PositiveInfinity;
        var text = string.Join(Environment.NewLine, elements.Select(x => x.Kind == PrompterElementKind.Comment ? $"// {x.Text}" : x.Text));
        var dpi = VisualTreeHelper.GetDpi(this);
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), _viewModel.FontSize, Brushes.White, dpi.PixelsPerDip) { MaxTextWidth = width };
        return formatted.Height;
    }

    /// <summary>Measures the current visible document using the overlay's WPF typeface and width.</summary>
    public double MeasureContentHeight(double width)
    {
        if (_viewModel is null || width <= 0) return 0;
        var dpi = VisualTreeHelper.GetDpi(this);
        var formatted = new FormattedText(_viewModel.Content, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), _viewModel.FontSize, Brushes.White, dpi.PixelsPerDip) { MaxTextWidth = width };
        return formatted.Height;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsInteractionLocked && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
