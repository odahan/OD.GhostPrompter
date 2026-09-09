using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using GhostPrompter.Models;
using GhostPrompter.Services;

namespace GhostPrompter.ViewModels;

/// <summary>Coordinates document loading, presentation state, navigation and visual settings.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IDocumentImportService _importService;
    private readonly ScriptParserService _parser;
    private readonly LoggingService? _logging;
    private readonly ScrollController _scroll = new();
    private PrompterDocument _document = PrompterDocument.Empty;
    private int _blockIndex;
    private int _pageIndex;
    private int _loadGeneration;
    private IReadOnlyList<PrompterPage> _pages = [];
    private CancellationTokenSource? _loadCancellation;

    public MainViewModel(IDocumentImportService importService, ScriptParserService parser, PrompterViewModel prompter, LoggingService? logging = null)
    {
        _importService = importService; _parser = parser; Prompter = prompter; _logging = logging;
        RefreshPresentation();
    }

    public PrompterViewModel Prompter { get; }
    public event EventHandler? PresentationChanged;
    public event EventHandler? RequestLoadFile;
    public event EventHandler? PresentationNeedsLayout;
    public event EventHandler? ScrollStateChanged;
    public event EventHandler? ResetWindowPositionRequested;
    public event EventHandler? RestoreDefaultShortcutsRequested;
    public bool IsScrollRunning => _scroll.IsRunning;
    public string PresentationStatus => IsPresentation
        ? $"Presentation · Click Through: {(ClickThroughPreferred ? "active" : "off")}" 
        : $"Configuration · Click Through preference: {(ClickThroughPreferred ? "on (inactive)" : "off")}";

    [ObservableProperty] private PrompterMode _mode = PrompterMode.Blocks;
    [ObservableProperty] private string _currentFile = "No script loaded";
    [ObservableProperty] private string _status = "Ready";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isPresentation;
    [ObservableProperty] private bool _clickThroughPreferred;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private bool _showTitles = true;
    [ObservableProperty] private bool _showProgress = true;
    [ObservableProperty] private double _fontSize = 30;
    [ObservableProperty] private double _backgroundOpacity = 75;
    [ObservableProperty] private double _textOpacity = 75;
    [ObservableProperty] private double _scrollSpeed = 50;
    [ObservableProperty] private double _startingHeight = 50;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _captureStatus = "Windows capture exclusion not initialized";
    [ObservableProperty] private string _hotkeyStatus = "Hotkeys not initialized";
    [ObservableProperty] private string _settingsStatus = string.Empty;

    partial void OnModeChanged(PrompterMode value) { _blockIndex = 0; _pageIndex = 0; ResetScrollLayout(); NotifyScrollState(); RefreshPresentation(); }
    partial void OnIsPresentationChanged(bool value) => OnPropertyChanged(nameof(PresentationStatus));
    partial void OnClickThroughPreferredChanged(bool value) => OnPropertyChanged(nameof(PresentationStatus));
    partial void OnProgressChanged(double value) => Prompter.Progress = value;
    partial void OnShowTitlesChanged(bool value) => RefreshPresentation();
    partial void OnShowProgressChanged(bool value) { Prompter.ShowProgress = value; RefreshPresentation(); }
    partial void OnFontSizeChanged(double value)
    {
        FontSize = Math.Clamp(value, 16, 72);
        Prompter.FontSize = FontSize;
        Pause();
        RefreshPresentation();
    }
    partial void OnBackgroundOpacityChanged(double value)
    {
        BackgroundOpacity = Math.Clamp(value, 0, 100);
        Prompter.BackgroundOpacity = BackgroundOpacity / 100;
        Prompter.RefreshBackgroundBrush();
    }
    partial void OnTextOpacityChanged(double value) { TextOpacity = Math.Clamp(value, 20, 100); Prompter.TextOpacity = TextOpacity / 100; }
    partial void OnStartingHeightChanged(double value) { StartingHeight = Math.Clamp(Math.Round(value / 5) * 5, 10, 80); Prompter.StartingHeight = StartingHeight; Pause(); }
    partial void OnScrollSpeedChanged(double value) { _scroll.SetSpeed(value); ScrollSpeed = _scroll.Speed; }

    [RelayCommand] private void Load() => RequestLoadFile?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void CancelLoad() => _loadCancellation?.Cancel();
    [RelayCommand] private async Task ReloadAsync()
    {
        if (CurrentFile is not "No script loaded" && File.Exists(CurrentFile)) await LoadPathAsync(CurrentFile);
        else Status = "No previous script is available to reload.";
    }
    public async Task LoadPathAsync(string path)
    {
        _loadCancellation?.Cancel(); _loadCancellation?.Dispose(); _loadCancellation = new CancellationTokenSource();
        var generation = ++_loadGeneration;
        IsLoading = true; Status = "Loading…"; Pause();
        try
        {
            var loaded = await _importService.LoadAsync(path, _loadCancellation.Token);
            if (generation != _loadGeneration) return;
            _document = loaded; _blockIndex = 0; _pageIndex = 0; _pages = []; ResetScrollLayout(); NotifyScrollState(); CurrentFile = path;
            Status = loaded.IsEmpty ? "The script is empty." : loaded.Warnings is { Count: > 0 } ? $"Script loaded. {string.Join(" ", loaded.Warnings)}" : "Script loaded.";
            _logging?.Write("INFO", $"Script loaded: {path}");
            RefreshPresentation();
        }
        catch (OperationCanceledException) when (generation == _loadGeneration) { Status = "Loading was cancelled."; _logging?.Write("INFO", $"Script loading was cancelled: {path}"); }
        catch (Exception exception) when (generation == _loadGeneration) { Status = $"Could not load script: {exception.Message}"; _logging?.Write("ERROR", $"Could not load script: {path}", exception); }
        finally { if (generation == _loadGeneration) IsLoading = false; }
    }

    [RelayCommand] private void NextPage()
    {
        if (Mode != PrompterMode.Blocks) return;
        if (_pages.Count > 0 && _pageIndex < _pages.Count - 1) { _pageIndex++; RefreshPresentation(); }
        else if (_pages.Count == 0 && _blockIndex < _document.Blocks.Count - 1) { _blockIndex++; RefreshPresentation(); }
    }
    [RelayCommand] private void PreviousPage()
    {
        if (Mode != PrompterMode.Blocks) return;
        if (_pages.Count > 0 && _pageIndex > 0) { _pageIndex--; RefreshPresentation(); }
        else if (_pages.Count == 0 && _blockIndex > 0) { _blockIndex--; RefreshPresentation(); }
    }
    [RelayCommand] private void TogglePlayPause() { if (_scroll.IsRunning) Pause(); else Play(); }
    [RelayCommand] private void Restart()
    {
        _scroll.Restart();
        Prompter.ScrollOffset = 0;
        Progress = 0;
        NotifyScrollState();
        RefreshPresentation();
    }
    [RelayCommand] private void IncreaseSpeed() { _scroll.ChangeSpeed(10); ScrollSpeed = _scroll.Speed; }
    [RelayCommand] private void DecreaseSpeed() { _scroll.ChangeSpeed(-10); ScrollSpeed = _scroll.Speed; }
    [RelayCommand] private void IncreaseText() => FontSize += 2;
    [RelayCommand] private void DecreaseText() => FontSize -= 2;
    [RelayCommand] private void ToggleVisibility() { IsVisible = !IsVisible; Pause(); PresentationChanged?.Invoke(this, EventArgs.Empty); }
    [RelayCommand] private void TogglePresentation() { IsPresentation = !IsPresentation; Pause(); PresentationChanged?.Invoke(this, EventArgs.Empty); }
    [RelayCommand] private void ToggleClickThrough() { ClickThroughPreferred = !ClickThroughPreferred; PresentationChanged?.Invoke(this, EventArgs.Empty); }
    [RelayCommand] private void ResetWindowPosition() => ResetWindowPositionRequested?.Invoke(this, EventArgs.Empty);
    [RelayCommand] private void RestoreDefaultShortcuts() => RestoreDefaultShortcutsRequested?.Invoke(this, EventArgs.Empty);

    public void AdvanceScroll(TimeSpan elapsed)
    {
        _scroll.Advance(elapsed); Progress = _scroll.Progress; Prompter.ScrollOffset = _scroll.Offset; Prompter.ProgressText = $"{Progress:P0}";
        if (!_scroll.IsRunning) NotifyScrollState();
    }
    /// <summary>Applies the current WPF text measurement to scrolling bounds and progress.</summary>
    public void ConfigureScrollLayout(double contentHeight, double usefulHeight)
    {
        if (Mode != PrompterMode.Scroll) return;
        if (_document.IsEmpty || !_document.Elements.Any(element => element.Kind != PrompterElementKind.Separator && IsVisibleElement(element)))
        {
            ResetScrollLayout();
            Progress = 0;
            Prompter.ProgressText = string.Empty;
            return;
        }
        var lineHeight = Math.Max(FontSize * 1.2, 1);
        var maximumOffset = Math.Max(0, contentHeight - lineHeight);
        _scroll.Configure(maximumOffset, contentHeight > 0);
        Pause();
        Progress = _scroll.Progress;
        Prompter.ScrollOffset = _scroll.Offset;
        Prompter.ProgressText = $"{Progress:P0}";
    }

    /// <summary>Applies WPF-measured page bounds after a visual layout change.</summary>
    public void ReflowBlocks(double availableHeight, Func<IReadOnlyList<PrompterElement>, double> measure)
    {
        if (Mode != PrompterMode.Blocks || _document.IsEmpty || availableHeight <= 0) return;
        var old = _pages.ElementAtOrDefault(_pageIndex);
        var pages = new List<PrompterPage>();
        var paginator = new PaginationService();
        foreach (var block in _document.Blocks)
        {
            var visible = block.Elements.Where(IsVisibleElement).ToArray();
            if (visible.Length == 0) continue;
            pages.AddRange(paginator.Paginate(new PrompterBlock(block.Index, visible), availableHeight, measure));
        }
        _pages = pages;
        _pageIndex = old is null ? 0 : Math.Clamp(pages.FindIndex(p => p.BlockIndex == old.BlockIndex && p.StartElement <= old.StartElement && p.EndElement >= old.StartElement), 0, Math.Max(0, pages.Count - 1));
        RefreshPresentation(requestLayout: false);
    }
    public void HandleHotkey(string action)
    {
        switch (action)
        {
            case "NextPage" when Mode == PrompterMode.Blocks: NextPageCommand.Execute(null); break;
            case "NextPage": IncreaseSpeedCommand.Execute(null); break;
            case "PreviousPage" when Mode == PrompterMode.Blocks: PreviousPageCommand.Execute(null); break;
            case "PreviousPage": DecreaseSpeedCommand.Execute(null); break;
            case "TogglePlayPause": TogglePlayPauseCommand.Execute(null); break;
            case "ToggleVisibility": ToggleVisibilityCommand.Execute(null); break;
            case "TogglePresentation": TogglePresentationCommand.Execute(null); break;
            case "ToggleClickThrough": ToggleClickThroughCommand.Execute(null); break;
            case "IncreaseText": IncreaseTextCommand.Execute(null); break;
            case "DecreaseText": DecreaseTextCommand.Execute(null); break;
            case "Restart": RestartCommand.Execute(null); break;
        }
    }
    public void SetCaptureStatus(string value) => CaptureStatus = value;
    public void SetHotkeyStatus(string value) => HotkeyStatus = value;
    public void SetSettingsStatus(string value) => SettingsStatus = value;

    /// <summary>Applies validated persisted preferences without automatically reopening the last source file.</summary>
    public void ApplySettings(AppSettings settings)
    {
        Mode = settings.Mode;
        ShowTitles = settings.ShowTitles;
        ShowProgress = settings.ShowProgress;
        FontSize = settings.FontSize;
        BackgroundOpacity = settings.BackgroundOpacity;
        TextOpacity = settings.TextOpacity;
        ScrollSpeed = settings.ScrollSpeed;
        StartingHeight = settings.StartingHeight;
        ClickThroughPreferred = settings.ClickThroughPreferred;
        if (!string.IsNullOrWhiteSpace(settings.LastFilePath)) CurrentFile = settings.LastFilePath;
        RefreshPresentation();
    }

    /// <summary>Builds the persisted preference snapshot for the current configuration.</summary>
    public AppSettings CreateSettings(double left, double top, double width, double height) => new()
    {
        Left = left,
        Top = top,
        Width = width,
        Height = height,
        BackgroundOpacity = BackgroundOpacity,
        TextOpacity = TextOpacity,
        FontSize = FontSize,
        Mode = Mode,
        ShowTitles = ShowTitles,
        ShowProgress = ShowProgress,
        ClickThroughPreferred = ClickThroughPreferred,
        ScrollSpeed = ScrollSpeed,
        StartingHeight = StartingHeight,
        LastFilePath = CurrentFile == "No script loaded" ? null : CurrentFile,
    };
    private void Play() { if (Mode == PrompterMode.Scroll) { _scroll.Play(); NotifyScrollState(); } }
    private void Pause() { _scroll.Pause(); NotifyScrollState(); }
    private void NotifyScrollState() => ScrollStateChanged?.Invoke(this, EventArgs.Empty);
    private void RefreshPresentation(bool requestLayout = true)
    {
        Prompter.IsScrollMode = Mode == PrompterMode.Scroll;
        Prompter.ShowProgress = ShowProgress;
        if (_document.IsEmpty)
        {
            ResetScrollLayout();
            Prompter.Content = "GhostPrompter is ready.\nLoad a TXT, Markdown or DOCX script.";
            Prompter.RenderElements =
            [
                new(PrompterElementKind.Text, "GhostPrompter is ready.", 0),
                new(PrompterElementKind.Text, "Load a TXT, Markdown or DOCX script.", 0),
            ];
            Prompter.ProgressText = string.Empty;
            return;
        }
        if (!_document.Elements.Any(element => element.Kind != PrompterElementKind.Separator && IsVisibleElement(element)))
        {
            Prompter.Content = string.Empty;
            Prompter.RenderElements = [];
            Prompter.ProgressText = string.Empty;
            Progress = 0;
            Status = "No visible content.";
            _scroll.Configure(0, false);
            NotifyScrollState();
            return;
        }
        if (Mode == PrompterMode.Blocks)
        {
            if (_pages.Count > 0)
            {
                var page = _pages[_pageIndex];
                var displayed = page.Elements;
                Prompter.Content = string.Join(Environment.NewLine, displayed.Select(DisplayText));
                Prompter.RenderElements = displayed;
                var totalInBlock = _pages.Count(x => x.BlockIndex == page.BlockIndex);
                var pageInBlock = _pages.Take(_pageIndex + 1).Count(x => x.BlockIndex == page.BlockIndex);
                Prompter.ProgressText = totalInBlock > 1 ? $"Block {page.BlockIndex + 1} / {_document.Blocks.Count} · Page {pageInBlock} / {totalInBlock}" : $"Block {page.BlockIndex + 1} / {_document.Blocks.Count}";
                Progress = (double)(_pageIndex + 1) / _pages.Count;
            }
            else
            {
                var block = _document.Blocks[_blockIndex];
                var displayed = block.Elements.Where(IsVisibleElement).ToArray();
                Prompter.Content = string.Join(Environment.NewLine, displayed.Select(DisplayText));
                Prompter.RenderElements = displayed;
                Prompter.ProgressText = $"Block {_blockIndex + 1} / {_document.Blocks.Count}";
                Progress = (double)(_blockIndex + 1) / _document.Blocks.Count;
            }
            if (requestLayout) PresentationNeedsLayout?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            var displayed = _document.Elements.Where(x => x.Kind == PrompterElementKind.Separator || IsVisibleElement(x)).ToArray();
            Prompter.Content = string.Join(Environment.NewLine, displayed.Select(DisplayText));
            Prompter.RenderElements = displayed;
            Progress = _scroll.Progress;
            Prompter.ProgressText = $"{Progress:P0}";
            PresentationNeedsLayout?.Invoke(this, EventArgs.Empty);
        }
    }
    private void ResetScrollLayout()
    {
        _scroll.Configure(0, false);
        _scroll.Restart();
        Prompter.ScrollOffset = 0;
        Progress = 0;
    }
    private bool IsVisibleElement(PrompterElement element) => ShowTitles || element.Kind != PrompterElementKind.Title;
    private static string DisplayText(PrompterElement element) => element.Text;
}
