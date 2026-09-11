using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
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
        if (known.HasValue && known.Value != NativeMethods.WdaExcludeFromCapture)
        {
            logging.Write("ERROR", $"Capture exclusion verification failed for {window.GetType().Name}.");
            return new CaptureExclusionResult(CaptureExclusionState.Failed, known, null);
        }
        logging.Write("INFO", $"Capture exclusion active for {window.GetType().Name}.");
        return new CaptureExclusionResult(CaptureExclusionState.Active, known, null);
    }
}

/// <summary>Preserves native WPF styles while applying no-activate and click-through behavior.</summary>
public sealed class WindowStyleService(LoggingService? logging = null)
{
    public bool ApplyPresentationStyles(Window window, bool clickThrough)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero) return false;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlExStyle).ToInt64() | NativeMethods.WsExNoActivate;
        style = clickThrough ? style | NativeMethods.WsExTransparent : style & ~NativeMethods.WsExTransparent;
        Marshal.SetLastPInvokeError(0);
        var previous = NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlExStyle, (nint)style);
        var error = Marshal.GetLastPInvokeError();
        if (previous == nint.Zero && error != 0)
        {
            logging?.Write("ERROR", $"Could not update prompter window styles (Windows error {error}).");
            return false;
        }
        return true;
    }
}

/// <summary>Registers native global hotkeys and reports independently failed combinations.</summary>
public sealed class GlobalHotkeyService : IDisposable
{
    public const uint ModAlt = 0x0001, ModControl = 0x0002, ModNoRepeat = 0x4000;
    private static readonly HashSet<string> SupportedActions =
    ["NextPage", "PreviousPage", "ScrollBackOnePage", "TogglePlayPause", "ToggleVisibility", "TogglePresentation", "ToggleClickThrough", "IncreaseText", "DecreaseText", "Restart"];
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
        new("NextPage", ModControl | ModAlt, 0x26), new("PreviousPage", ModControl | ModAlt, 0x28), new("ScrollBackOnePage", ModControl | ModAlt, 0x25), new("TogglePlayPause", ModControl | ModAlt, 0x50),
        new("ToggleVisibility", ModControl | ModAlt, 0x20), new("TogglePresentation", ModControl | ModAlt, 0x4C), new("ToggleClickThrough", ModControl | ModAlt, 0x54),
        new("IncreaseText", ModControl | ModAlt, 0x6B), new("DecreaseText", ModControl | ModAlt, 0x6D), new("Restart", ModControl | ModAlt, 0x24),
        new("IncreaseText", ModControl | ModAlt, 0xBB), new("DecreaseText", ModControl | ModAlt, 0xBD),
    ];

    /// <summary>Registers defaults or validated persisted replacements, reporting every independent failure.</summary>
    public void Register(IEnumerable<HotkeySettings>? settings)
    {
        Suspend();
        _status.Clear(); _activeSettings.Clear();
        var selected = CompleteWithNewDefaults(settings);
        var duplicates = new HashSet<(uint Modifiers, uint Key)>();
        var id = 1;
        foreach (var setting in selected)
        {
            if (setting is null || string.IsNullOrWhiteSpace(setting.Action) || setting.Key is 0 or > 0xFF
                || (setting.Modifiers & ~(ModAlt | ModControl)) != 0)
            {
                _status[$"Invalid shortcut {id++}"] = "Invalid shortcut in settings";
                _logging?.Write("ERROR", "Invalid hotkey entry in settings.");
                continue;
            }
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

    private static IReadOnlyList<HotkeySettings> CompleteWithNewDefaults(IEnumerable<HotkeySettings>? settings)
    {
        var selected = settings?.ToList() ?? [];
        if (selected.Count == 0) return DefaultSettings;

        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var setting in DefaultSettings)
        {
            var occurrence = occurrences.GetValueOrDefault(setting.Action);
            occurrences[setting.Action] = occurrence + 1;
            var configuredCount = selected.Count(candidate => candidate.Action == setting.Action);
            if (configuredCount <= occurrence) selected.Add(setting);
        }
        return selected;
    }

    private void Register(int id, HotkeySettings setting)
    {
        if (NativeMethods.RegisterHotKey(_handle, id, setting.Modifiers | ModNoRepeat, setting.Key))
        {
            _registered[id] = setting.Action;
            _status[$"{GetActionDisplayName(setting.Action)} — {FormatShortcut(setting)}"] = "Registered";
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            _status[$"{GetActionDisplayName(setting.Action)} — {FormatShortcut(setting)}"] = $"Unavailable (Windows error {error})";
            _logging?.Write("ERROR", $"Hotkey {FormatShortcut(setting)} for {setting.Action} could not be registered (Windows error {error}).");
        }
    }

    /// <summary>Temporarily unregisters shortcuts while preserving their editable settings.</summary>
    public void Suspend()
    {
        foreach (var id in _registered.Keys) NativeMethods.UnregisterHotKey(_handle, id);
        _registered.Clear();
    }

    /// <summary>Formats a shortcut using names intended for the settings interface.</summary>
    public static string FormatShortcut(HotkeySettings setting)
    {
        var parts = new List<string>();
        if ((setting.Modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((setting.Modifiers & ModAlt) != 0) parts.Add("Alt");
        parts.Add(FormatKey(setting.Key));
        return string.Join(" + ", parts);
    }

    /// <summary>Returns a user-facing name for a shortcut action.</summary>
    public static string GetActionDisplayName(string action) => action switch
    {
        "NextPage" => "Next page / Faster",
        "PreviousPage" => "Previous page / Slower",
        "ScrollBackOnePage" => "Back one page (Scroll)",
        "TogglePlayPause" => "Play or pause",
        "ToggleVisibility" => "Show or hide prompter",
        "TogglePresentation" => "Configuration or Presentation",
        "ToggleClickThrough" => "Click Through",
        "IncreaseText" => "Increase text size",
        "DecreaseText" => "Decrease text size",
        "Restart" => "First page or restart",
        _ => action,
    };

    private static string FormatKey(uint virtualKey)
    {
        if (virtualKey is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A) return ((char)virtualKey).ToString();
        if (virtualKey is >= 0x60 and <= 0x69) return $"Numpad {virtualKey - 0x60}";
        if (virtualKey is >= 0x70 and <= 0x87) return $"F{virtualKey - 0x6F}";
        return virtualKey switch
        {
            0x08 => "Backspace",
            0x09 => "Tab",
            0x0D => "Enter",
            0x20 => "Space",
            0x21 => "Page Up",
            0x22 => "Page Down",
            0x23 => "End",
            0x24 => "Home",
            0x25 => "Left Arrow",
            0x26 => "Up Arrow",
            0x27 => "Right Arrow",
            0x28 => "Down Arrow",
            0x2D => "Insert",
            0x2E => "Delete",
            0x6A => "Numpad ×",
            0x6B => "Numpad +",
            0x6D => "Numpad −",
            0x6F => "Numpad ÷",
            0xBA => ";",
            0xBB => "+",
            0xBC => ",",
            0xBD => "−",
            0xBE => ".",
            _ => KeyInterop.KeyFromVirtualKey((int)virtualKey).ToString(),
        };
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WmHotkey && _registered.TryGetValue(wParam.ToInt32(), out var action)) { Triggered?.Invoke(this, action); handled = true; }
        return nint.Zero;
    }
    public void Dispose() => Suspend();
}
