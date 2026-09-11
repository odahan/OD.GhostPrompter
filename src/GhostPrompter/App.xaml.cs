using System.Windows;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using GhostPrompter.Services;
using GhostPrompter.ViewModels;
using GhostPrompter.Native;
using System.Windows.Interop;

namespace GhostPrompter;

/// <summary>Owns application-wide services and lifetime of the two protected windows.</summary>
public partial class App : Application
{
    private readonly LoggingService _logging = new();
    private GlobalHotkeyService? _hotkeys;
    private PrompterWindow? _prompterWindow;
    private MainViewModel? _viewModel;
    private readonly Stopwatch _scrollClock = new();
    private readonly SettingsService _settings;
    private readonly DispatcherTimer _settingsSaveTimer = new() { Interval = TimeSpan.FromMilliseconds(600) };
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;
    private Models.CaptureExclusionResult _mainCapture = Models.CaptureExclusionResult.NotInitialized;
    private Models.CaptureExclusionResult _prompterCapture = Models.CaptureExclusionResult.NotInitialized;
    private bool _isRenderingScroll;
    private bool _settingsInitialized;
    private DispatcherOperation? _reflowOperation;

    public App() => _settings = new SettingsService(logging: _logging);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\GhostPrompter", out var createdNew);
        _ownsSingleInstanceMutex = createdNew;
        if (!createdNew)
        {
            MessageBox.Show("GhostPrompter is already running.", "GhostPrompter", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        _logging.Start();
        _logging.Write("INFO", $"GhostPrompter started. Application {GetType().Assembly.GetName().Version}; {Environment.OSVersion.VersionString}.");
        DispatcherUnhandledException += (_, args) => _logging.Write("FATAL", "Unhandled UI exception.", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) => _logging.Write("FATAL", "Unhandled domain exception.", args.ExceptionObject as Exception);
        var parser = new ScriptParserService();
        var main = new MainWindow();
        _viewModel = new MainViewModel(new DocumentImportService(parser), parser, new PrompterViewModel(), _logging);
        if (!_logging.IsAvailable) _viewModel.SetSettingsStatus("Local logging is unavailable.");
        main.DataContext = _viewModel;
        _prompterWindow = new PrompterWindow { DataContext = _viewModel.Prompter };
        _viewModel.RequestLoadFile += main.OnLoadRequested;
        _viewModel.PresentationChanged += (_, _) => ApplyWindowState();
        _viewModel.ScrollStateChanged += (_, _) => UpdateScrollRendering();
        _viewModel.ResetWindowPositionRequested += (_, _) => ResetPrompterWindowPosition();
        _viewModel.ShortcutSettingsRequested += (_, _) => OpenShortcutSettings(main);
        _viewModel.TextEditorRequested += async (_, _) => await OpenTextEditorAsync(main);
        _viewModel.PresentationNeedsLayout += (_, _) => QueueReflow();
        _prompterWindow.ReflowRequested += (_, _) => QueueReflow();
        _viewModel.PropertyChanged += (_, _) => ScheduleSettingsSave();
        _settingsSaveTimer.Tick += async (_, _) =>
        {
            _settingsSaveTimer.Stop();
            await SaveSettingsAsync();
        };
        main.SourceInitialized += (_, _) =>
        {
            _mainCapture = new CaptureExclusionService(_logging).Apply(main);
            UpdateCaptureStatus();
            _hotkeys = new GlobalHotkeyService(main, _logging); _hotkeys.Register(null);
            _hotkeys.Triggered += (_, action) => _viewModel.HandleHotkey(action);
            UpdateHotkeyStatus();
        };
        _prompterWindow.SourceInitialized += (_, _) =>
        {
            _prompterCapture = new CaptureExclusionService(_logging).Apply(_prompterWindow);
            UpdateCaptureStatus();
        };
        main.Closed += (_, _) => Shutdown();
        _prompterWindow.LocationChanged += (_, _) => ScheduleSettingsSave();
        _prompterWindow.SizeChanged += (_, _) => ScheduleSettingsSave();
        main.Show(); _prompterWindow.Show(); ApplyWindowState();
        _ = InitializeSettingsAsync();
    }

    private void ApplyWindowState()
    {
        if (_viewModel is null || _prompterWindow is null) return;
        if (_viewModel.IsVisible) _prompterWindow.Show(); else _prompterWindow.Hide();
        var stylesApplied = new WindowStyleService(_logging).ApplyPresentationStyles(_prompterWindow, _viewModel.IsPresentation && _viewModel.ClickThroughPreferred);
        _viewModel.SetWindowStatus(stylesApplied ? "Window interaction styles active" : "Window interaction styles could not be applied");
        _prompterWindow.ResizeMode = _viewModel.IsPresentation ? ResizeMode.NoResize : ResizeMode.CanResizeWithGrip;
        _prompterWindow.IsInteractionLocked = _viewModel.IsPresentation;
    }

    private void ReflowBlocks()
    {
        if (_viewModel is null || _prompterWindow is null) return;
        var width = _prompterWindow.ContentGridWidth;
        var height = _prompterWindow.ContentGridHeight;
        if (width <= 0 || height <= 0) return;
        if (_viewModel.Mode == Models.PrompterMode.Scroll)
        {
            _prompterWindow.UpdateLayout();
            _viewModel.ConfigureScrollLayout(_prompterWindow.ScrollableHeight, _prompterWindow.ContentGridHeight);
            return;
        }
        _viewModel.ReflowBlocks(height, elements => _prompterWindow.MeasureElements(elements, width));
    }

    private void QueueReflow()
    {
        if (_prompterWindow is null) return;
        if (_reflowOperation?.Status == DispatcherOperationStatus.Pending) _reflowOperation.Abort();
        _reflowOperation = _prompterWindow.Dispatcher.BeginInvoke(ReflowBlocks, DispatcherPriority.Render);
    }

    private void ResetPrompterWindowPosition()
    {
        if (_prompterWindow is null) return;
        _prompterWindow.Width = Math.Clamp(_prompterWindow.Width, _prompterWindow.MinWidth, SystemParameters.WorkArea.Width);
        _prompterWindow.Height = Math.Clamp(_prompterWindow.Height, _prompterWindow.MinHeight, SystemParameters.WorkArea.Height);
        _prompterWindow.Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - _prompterWindow.Width) / 2);
        _prompterWindow.Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - _prompterWindow.Height) / 2);
    }

    private bool IsPrompterWindowReachable()
    {
        if (_prompterWindow is null) return false;
        var handle = new WindowInteropHelper(_prompterWindow).Handle;
        if (handle == nint.Zero || !NativeMethods.GetWindowRect(handle, out var windowRectangle)) return false;
        const int minimumVisibleArea = 50;
        var reachable = false;
        NativeMethods.MonitorEnumProc callback = (nint monitor, nint deviceContext, ref NativeMethods.Rect monitorRectangle, nint data) =>
        {
            var intersectionWidth = Math.Min(windowRectangle.Right, monitorRectangle.Right) - Math.Max(windowRectangle.Left, monitorRectangle.Left);
            var intersectionHeight = Math.Min(windowRectangle.Bottom, monitorRectangle.Bottom) - Math.Max(windowRectangle.Top, monitorRectangle.Top);
            reachable = intersectionWidth >= minimumVisibleArea && intersectionHeight >= minimumVisibleArea;
            return !reachable;
        };
        return NativeMethods.EnumDisplayMonitors(nint.Zero, nint.Zero, callback, nint.Zero) && reachable;
    }

    private void OpenShortcutSettings(MainWindow owner)
    {
        if (_hotkeys is null) return;
        var originalSettings = _hotkeys.ActiveSettings.ToArray();
        _hotkeys.Suspend();
        var window = new ShortcutSettingsWindow(originalSettings) { Owner = owner };
        window.SourceInitialized += (_, _) => new CaptureExclusionService(_logging).Apply(window);
        var saved = false;
        try
        {
            saved = window.ShowDialog() == true;
        }
        finally
        {
            _hotkeys.Register(saved ? window.Result : originalSettings);
            UpdateHotkeyStatus();
        }
        if (saved) ScheduleSettingsSave();
    }

    private async Task OpenTextEditorAsync(MainWindow owner)
    {
        if (_viewModel is null || !_viewModel.CanEditText) return;
        var activeHotkeys = _hotkeys?.ActiveSettings.ToArray();
        _hotkeys?.Suspend();
        var window = new TextEditorWindow(_viewModel.EditableText, _viewModel.CurrentFile) { Owner = owner };
        window.SourceInitialized += (_, _) => new CaptureExclusionService(_logging).Apply(window);
        try
        {
            if (window.ShowDialog() == true && window.SavedPath is { } savedPath)
                await _viewModel.LoadPathAsync(savedPath);
        }
        finally
        {
            if (activeHotkeys is not null)
            {
                _hotkeys?.Register(activeHotkeys);
                UpdateHotkeyStatus();
            }
        }
    }

    private void UpdateHotkeyStatus()
    {
        if (_hotkeys is null || _viewModel is null) return;
        var failures = _hotkeys.RegistrationStatus.Where(status => status.Value != "Registered").ToArray();
        _viewModel.SetHotkeyStatus(failures.Length == 0
            ? $"All {_hotkeys.RegistrationStatus.Count} keyboard shortcuts are active."
            : $"Unavailable shortcuts: {string.Join("; ", failures.Select(status => $"{status.Key}: {status.Value}"))}");
    }

    private void UpdateCaptureStatus()
    {
        _viewModel?.SetCaptureStatus($"Main window: {_mainCapture.State} · Prompter window: {_prompterCapture.State}");
    }

    private void UpdateScrollRendering()
    {
        if (_viewModel?.IsScrollRunning == true && !_isRenderingScroll)
        {
            _isRenderingScroll = true; _scrollClock.Restart(); CompositionTarget.Rendering += OnRendering;
        }
        else if (_viewModel?.IsScrollRunning != true && _isRenderingScroll)
        {
            CompositionTarget.Rendering -= OnRendering; _scrollClock.Reset(); _isRenderingScroll = false;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (_viewModel is null) return;
        var elapsed = _scrollClock.Elapsed; _scrollClock.Restart();
        if (elapsed > TimeSpan.FromSeconds(1)) { _viewModel.TogglePlayPauseCommand.Execute(null); return; }
        _viewModel.AdvanceScroll(elapsed);
    }

    private async Task InitializeSettingsAsync()
    {
        if (_viewModel is null || _prompterWindow is null) return;
        var settings = await _settings.LoadAsync();
        _hotkeys?.Register(settings.Hotkeys);
        UpdateHotkeyStatus();
        _viewModel.ApplySettings(settings);
        if (double.IsFinite(settings.Left)) _prompterWindow.Left = settings.Left;
        if (double.IsFinite(settings.Top)) _prompterWindow.Top = settings.Top;
        _prompterWindow.Width = Math.Clamp(settings.Width, _prompterWindow.MinWidth, SystemParameters.WorkArea.Width);
        _prompterWindow.Height = Math.Clamp(settings.Height, _prompterWindow.MinHeight, SystemParameters.WorkArea.Height);
        if (!IsPrompterWindowReachable()) ResetPrompterWindowPosition();
        ApplyWindowState();
        _settingsInitialized = true;
    }

    private void ScheduleSettingsSave()
    {
        // Window creation and bindings can change properties before persisted settings are loaded.
        // Do not let that transient state overwrite the user's settings file.
        if (!_settingsInitialized) return;
        _settingsSaveTimer.Stop();
        _settingsSaveTimer.Start();
    }

    private async Task SaveSettingsAsync()
    {
        if (_viewModel is null || _prompterWindow is null) return;
        try
        {
            var snapshot = _viewModel.CreateSettings(_prompterWindow.Left, _prompterWindow.Top, _prompterWindow.Width, _prompterWindow.Height);
            snapshot.Hotkeys = _hotkeys?.ActiveSettings.ToList() ?? [];
            await _settings.SaveAsync(snapshot);
            _viewModel.SetSettingsStatus(string.Empty);
        }
        catch (Exception exception)
        {
            _logging.Write("ERROR", "Could not save settings.", exception);
            _viewModel.SetSettingsStatus("Settings could not be saved. Changes remain active for this session.");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_isRenderingScroll) CompositionTarget.Rendering -= OnRendering;
        _settingsSaveTimer.Stop();
        if (_settingsInitialized && _viewModel is not null && _prompterWindow is not null)
        {
            var snapshot = _viewModel.CreateSettings(_prompterWindow.Left, _prompterWindow.Top, _prompterWindow.Width, _prompterWindow.Height);
            snapshot.Hotkeys = _hotkeys?.ActiveSettings.ToList() ?? [];
            try { Task.Run(() => _settings.SaveAsync(snapshot)).GetAwaiter().GetResult(); }
            catch (Exception exception) { _logging.Write("ERROR", "Could not save settings during shutdown.", exception); }
        }
        _hotkeys?.Dispose(); _logging.Write("INFO", "GhostPrompter closed."); _logging.Dispose(); base.OnExit(e);
        if (_ownsSingleInstanceMutex) _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
    }
}
