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
    private PrompterViewModel? _viewModel;
    public bool IsInteractionLocked { get; set; }
    public event EventHandler? ReflowRequested;
    public double ContentGridWidth => ContentText.ActualWidth;
    /// <summary>Gets the height reserved for readable text.</summary>
    public double ContentGridHeight => ContentText.ActualHeight;
    /// <summary>Gets the current scroll extent calculated by WPF.</summary>
    public double ScrollableHeight => Math.Max(0, ContentText.ExtentHeight - ContentText.ViewportHeight);

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
        if (e.PropertyName is nameof(PrompterViewModel.RenderElements)
            or nameof(PrompterViewModel.FontSize) or nameof(PrompterViewModel.StartingHeight)
            or nameof(PrompterViewModel.IsScrollMode) or nameof(PrompterViewModel.ShowProgress))
        {
            BuildDocument();
            QueueVisualUpdate();
        }
        if (e.PropertyName == nameof(PrompterViewModel.ScrollOffset)) QueueVisualUpdate();
    }

    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        BuildDocument();
        QueueVisualUpdate();
        ReflowRequested?.Invoke(this, EventArgs.Empty);
    }
    private void QueueVisualUpdate() => Dispatcher.BeginInvoke(UpdateVisualLayout, DispatcherPriority.Render);
    private void UpdateVisualLayout()
    {
        if (_viewModel is null || ContentGridHeight <= 0) return;
        ContentText.ScrollToVerticalOffset(_viewModel.IsScrollMode ? _viewModel.ScrollOffset : 0);
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
        var topPadding = _viewModel.IsScrollMode ? GetScrollAnchor() : 0;
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0, topPadding, 0, 0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = _viewModel.FontSize,
            Foreground = Brushes.White,
            ColumnWidth = double.PositiveInfinity,
        };
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
        ContentText.UpdateLayout();
        if (_viewModel.IsScrollMode && document.ContentEnd.GetInsertionPosition(LogicalDirection.Backward) is { } end)
        {
            var lastLineHeight = Math.Max(1, end.GetCharacterRect(LogicalDirection.Backward).Height);
            document.PagePadding = new Thickness(0, topPadding, 0, Math.Max(0, ContentGridHeight - topPadding - lastLineHeight));
            ContentText.UpdateLayout();
        }
    }

    /// <summary>Measures parsed elements with the same WPF text engine used by the overlay.</summary>
    public double MeasureElements(IReadOnlyList<PrompterElement> elements, double width)
    {
        if (_viewModel is null || width <= 0) return double.PositiveInfinity;
        var text = string.Join(Environment.NewLine, elements.Select(x => x.Text));
        var dpi = VisualTreeHelper.GetDpi(this);
        var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), _viewModel.FontSize, Brushes.White, dpi.PixelsPerDip) { MaxTextWidth = width };
        var offset = 0;
        foreach (var element in elements)
        {
            if (element.Kind == PrompterElementKind.Title) formatted.SetFontWeight(FontWeights.Bold, offset, element.Text.Length);
            if (element.Kind == PrompterElementKind.Comment) formatted.SetFontStyle(FontStyles.Italic, offset, element.Text.Length);
            offset += element.Text.Length + 1;
        }
        return formatted.Height;
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsInteractionLocked && e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}
