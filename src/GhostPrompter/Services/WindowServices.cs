using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GhostPrompter.Models;
using GhostPrompter.Native;

namespace GhostPrompter.Services;

/// <summary>Applies Windows capture exclusion after a WPF window owns an HWND.</summary>
public sealed class CaptureExclusionService(LoggingService logging)
{
    public CaptureExclusionResult Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return CaptureExclusionResult.NotInitialized;
        if (!NativeMethods.SetWindowDisplayAffinity(handle, NativeMethods.WdaExcludeFromCapture))
        {
            var error = Marshal.GetLastWin32Error();
            logging.Write("ERROR", $"Capture exclusion failed for {window.GetType().Name} ({error}).");
            return new CaptureExclusionResult(error == 87 ? CaptureExclusionState.Unavailable : CaptureExclusionState.Failed, null, error);
        }
        uint? known = NativeMethods.GetWindowDisplayAffinity(handle, out var affinity) ? affinity : null;
        logging.Write("INFO", $"Capture exclusion active for {window.GetType().Name}.");
        return new CaptureExclusionResult(CaptureExclusionState.Active, known, null);
    }
}

/// <summary>Preserves native WPF styles while applying no-activate and click-through behavior.</summary>
public sealed class WindowStyleService
{
    public void ApplyPresentationStyles(Window window, bool clickThrough)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64() | NativeMethods.WsExNoActivate;
        style = clickThrough ? style | NativeMethods.WsExTransparent : style & ~NativeMethods.WsExTransparent;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, (nint)style);
    }
}

/// <summary>Registers native global hotkeys and reports independently failed combinations.</summary>
public sealed class GlobalHotkeyService : IDisposable
{
    public const uint ModAlt = 0x0001, ModControl = 0x0002, ModNoRepeat = 0x4000;
    private static readonly HashSet<string> SupportedActions =
    ["NextPage", "PreviousPage", "TogglePlayPause", "ToggleVisibility", "TogglePresentation", "ToggleClickThrough", "IncreaseText", "DecreaseText", "Restart"];
    private readonly Dictionary<int, string> _registered = [];
    private readonly List<HotkeySettings> _activeSettings = [];
    private readonly nint _handle;
    private readonly LoggingService? _logging;
    public event EventHandler<string>? Triggered;
    public IReadOnlyDictionary<string, string> RegistrationStatus => _status;
    public IReadOnlyList<HotkeySettings> ActiveSettings => _activeSettings;
    private readonly Dictionary<string, string> _status = [];

    public GlobalHotkeyService(Window window, LoggingService? logging = null)
    {
        _logging = logging;
        var source = (HwndSource)PresentationSource.FromVisual(window)!;
        _handle = source.Handle;
        source.AddHook(WndProc);
    }

    public static IReadOnlyList<HotkeySettings> DefaultSettings { get; } =
    [
        new("NextPage", ModControl | ModAlt, 0x26), new("PreviousPage", ModControl | ModAlt, 0x28), new("TogglePlayPause", ModControl | ModAlt, 0x50),
        new("ToggleVisibility", ModControl | ModAlt, 0x20), new("TogglePresentation", ModControl | ModAlt, 0x4C), new("ToggleClickThrough", ModControl | ModAlt, 0x54),
        new("IncreaseText", ModControl | ModAlt, 0x6B), new("DecreaseText", ModControl | ModAlt, 0x6D), new("Restart", ModControl | ModAlt, 0x24),
        new("IncreaseText", ModControl | ModAlt, 0xBB), new("DecreaseText", ModControl | ModAlt, 0xBD),
    ];

    /// <summary>Registers defaults or validated persisted replacements, reporting every independent failure.</summary>
    public void Register(IEnumerable<HotkeySettings>? settings)
    {
        foreach (var registeredId in _registered.Keys) NativeMethods.UnregisterHotKey(_handle, registeredId);
        _registered.Clear(); _status.Clear(); _activeSettings.Clear();
        var selected = settings?.ToArray() is { Length: > 0 } configured ? configured : DefaultSettings;
        var duplicates = new HashSet<(uint Modifiers, uint Key)>();
        var id = 1;
        foreach (var setting in selected)
        {
            if (!SupportedActions.Contains(setting.Action))
            {
                _status[setting.Action] = "Unknown action in settings";
                _logging?.Write("ERROR", $"Unknown hotkey action: {setting.Action}.");
                continue;
            }
            if (!duplicates.Add((setting.Modifiers, setting.Key)))
            {
                _status[setting.Action] = "Duplicate shortcut in settings";
                _logging?.Write("ERROR", $"Duplicate hotkey setting for {setting.Action}.");
                continue;
            }
            // Keep a valid user preference even if another process currently owns it.
            // Otherwise the next automatic settings save would silently discard it.
            _activeSettings.Add(setting);
            Register(id++, setting);
        }
    }

    private void Register(int id, HotkeySettings setting)
    {
        if (NativeMethods.RegisterHotKey(_handle, id, setting.Modifiers | ModNoRepeat, setting.Key))
        {
            _registered[id] = setting.Action;
            _status[$"{setting.Action} ({FormatShortcut(setting)})"] = "Registered";
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            _status[$"{setting.Action} ({FormatShortcut(setting)})"] = $"Unavailable (Windows error {error})";
            _logging?.Write("ERROR", $"Hotkey {FormatShortcut(setting)} for {setting.Action} could not be registered (Windows error {error}).");
        }
    }

    private static string FormatShortcut(HotkeySettings setting) => $"0x{setting.Modifiers:X}+0x{setting.Key:X}";

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && _registered.TryGetValue(wParam.ToInt32(), out var action)) { Triggered?.Invoke(this, action); handled = true; }
        return nint.Zero;
    }
    public void Dispose() { foreach (var id in _registered.Keys) NativeMethods.UnregisterHotKey(_handle, id); _registered.Clear(); }
}
