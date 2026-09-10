using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GhostPrompter.Models;
using GhostPrompter.Services;

namespace GhostPrompter.ViewModels;

/// <summary>Represents one editable global shortcut in the settings window.</summary>
public sealed partial class ShortcutBindingViewModel : ObservableObject
{
    public ShortcutBindingViewModel(string action, string displayName, uint modifiers, uint key)
    {
        Action = action;
        DisplayName = displayName;
        _modifiers = modifiers;
        _key = key;
    }

    public string Action { get; }
    public string DisplayName { get; }
    public string ShortcutText => GlobalHotkeyService.FormatShortcut(new HotkeySettings(Action, Modifiers, Key));
    public string ChangeButtonText => IsCapturing ? "Press shortcut…" : "Change";

    [ObservableProperty] private uint _modifiers;
    [ObservableProperty] private uint _key;
    [ObservableProperty] private bool _isCapturing;

    partial void OnModifiersChanged(uint value) => OnPropertyChanged(nameof(ShortcutText));
    partial void OnKeyChanged(uint value) => OnPropertyChanged(nameof(ShortcutText));
    partial void OnIsCapturingChanged(bool value) => OnPropertyChanged(nameof(ChangeButtonText));

    /// <summary>Replaces the captured key combination.</summary>
    public void SetShortcut(uint modifiers, uint key)
    {
        Modifiers = modifiers;
        Key = key;
        IsCapturing = false;
    }

    /// <summary>Creates the persisted shortcut value.</summary>
    public HotkeySettings ToSettings() => new(Action, Modifiers, Key);
}

/// <summary>Coordinates shortcut editing, duplicate validation, and default restoration.</summary>
public sealed partial class ShortcutSettingsViewModel : ObservableObject
{
    public ShortcutSettingsViewModel(IEnumerable<HotkeySettings> currentSettings) => Load(currentSettings);

    public ObservableCollection<ShortcutBindingViewModel> Shortcuts { get; } = [];

    [ObservableProperty] private string _message = "Select Change, then press a shortcut containing Ctrl or Alt.";
    [ObservableProperty] private bool _hasError;

    /// <summary>Restores every built-in shortcut in the editor.</summary>
    public void RestoreDefaults()
    {
        Load(GlobalHotkeyService.DefaultSettings);
        HasError = false;
        Message = "Default shortcuts restored. Select Save to apply them.";
    }

    /// <summary>Validates the editor and returns a complete shortcut snapshot.</summary>
    public bool TryCreateSettings(out IReadOnlyList<HotkeySettings> settings)
    {
        var values = Shortcuts.Select(shortcut => shortcut.ToSettings()).ToArray();
        var duplicate = values.GroupBy(value => (value.Modifiers, value.Key)).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            HasError = true;
            Message = $"The shortcut {GlobalHotkeyService.FormatShortcut(duplicate.First())} is assigned more than once.";
            settings = [];
            return false;
        }

        HasError = false;
        settings = values;
        return true;
    }

    private void Load(IEnumerable<HotkeySettings> settings)
    {
        var configured = settings.ToList();
        if (configured.Count == 0) configured = GlobalHotkeyService.DefaultSettings.ToList();
        var occurrenceByAction = new Dictionary<string, int>(StringComparer.Ordinal);
        var configuredByAction = configured.GroupBy(setting => setting.Action, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        Shortcuts.Clear();
        foreach (var defaultSetting in GlobalHotkeyService.DefaultSettings)
        {
            var occurrence = occurrenceByAction.GetValueOrDefault(defaultSetting.Action);
            occurrenceByAction[defaultSetting.Action] = occurrence + 1;
            var value = configuredByAction.TryGetValue(defaultSetting.Action, out var candidates) && occurrence < candidates.Count
                ? candidates[occurrence]
                : defaultSetting;
            var alternativeCount = GlobalHotkeyService.DefaultSettings.Count(setting => setting.Action == defaultSetting.Action);
            var name = GlobalHotkeyService.GetActionDisplayName(defaultSetting.Action);
            if (alternativeCount > 1) name += $" (shortcut {occurrence + 1})";
            Shortcuts.Add(new ShortcutBindingViewModel(defaultSetting.Action, name, value.Modifiers, value.Key));
        }
    }
}
