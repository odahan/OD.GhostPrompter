using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GhostPrompter.Models;
using GhostPrompter.Services;
using GhostPrompter.ViewModels;

namespace GhostPrompter;

/// <summary>Captures and validates user-friendly global shortcut assignments.</summary>
public partial class ShortcutSettingsWindow : Window
{
    private ShortcutBindingViewModel? _capturingShortcut;

    public ShortcutSettingsWindow(IEnumerable<HotkeySettings> settings)
    {
        InitializeComponent();
        ViewModel = new ShortcutSettingsViewModel(settings);
        DataContext = ViewModel;
    }

    public ShortcutSettingsViewModel ViewModel { get; }
    public IReadOnlyList<HotkeySettings> Result { get; private set; } = [];

    private void OnChangeShortcutClick(object sender, RoutedEventArgs e)
    {
        CancelCapture();
        if (sender is not Button { DataContext: ShortcutBindingViewModel shortcut }) return;
        _capturingShortcut = shortcut;
        shortcut.IsCapturing = true;
        ViewModel.HasError = false;
        ViewModel.Message = $"Press the new shortcut for {shortcut.DisplayName}, or press Escape to cancel.";
        Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_capturingShortcut is null) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            CancelCapture();
            ViewModel.Message = "Shortcut change cancelled.";
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;

        var keyboardModifiers = Keyboard.Modifiers;
        if ((keyboardModifiers & (ModifierKeys.Shift | ModifierKeys.Windows)) != 0)
        {
            ViewModel.HasError = true;
            ViewModel.Message = "Use Ctrl or Alt modifiers; Shift and the Windows key are not supported.";
            return;
        }
        uint modifiers = 0;
        if ((keyboardModifiers & ModifierKeys.Control) != 0) modifiers |= GlobalHotkeyService.ModControl;
        if ((keyboardModifiers & ModifierKeys.Alt) != 0) modifiers |= GlobalHotkeyService.ModAlt;
        if (modifiers == 0)
        {
            ViewModel.HasError = true;
            ViewModel.Message = "The shortcut must contain Ctrl or Alt.";
            return;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey is 0 or > 0xFF)
        {
            ViewModel.HasError = true;
            ViewModel.Message = "That key cannot be used as a global shortcut.";
            return;
        }
        _capturingShortcut.SetShortcut(modifiers, virtualKey);
        ViewModel.HasError = false;
        ViewModel.Message = "Shortcut updated. Select Save to apply your changes.";
        _capturingShortcut = null;
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        CancelCapture();
        ViewModel.RestoreDefaults();
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        CancelCapture();
        if (!ViewModel.TryCreateSettings(out var settings)) return;
        Result = settings;
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => DialogResult = false;
    private void OnCloseClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void CancelCapture()
    {
        if (_capturingShortcut is not null) _capturingShortcut.IsCapturing = false;
        _capturingShortcut = null;
    }
}
