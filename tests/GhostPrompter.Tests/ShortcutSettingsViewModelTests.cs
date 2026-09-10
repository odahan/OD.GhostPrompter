using GhostPrompter.Models;
using GhostPrompter.Services;
using GhostPrompter.ViewModels;

namespace GhostPrompter.Tests;

/// <summary>Tests human-readable shortcut editing and validation.</summary>
public sealed class ShortcutSettingsViewModelTests
{
    [Fact]
    public void Constructor_ExposesEveryDefaultShortcutWithReadableNames()
    {
        var viewModel = new ShortcutSettingsViewModel(GlobalHotkeyService.DefaultSettings);

        Assert.Equal(GlobalHotkeyService.DefaultSettings.Count, viewModel.Shortcuts.Count);
        Assert.Contains(viewModel.Shortcuts, shortcut => shortcut.DisplayName == "Play or pause" && shortcut.ShortcutText == "Ctrl + Alt + P");
        Assert.DoesNotContain(viewModel.Shortcuts, shortcut => shortcut.ShortcutText.Contains("0x", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TryCreateSettings_RejectsDuplicateKeyCombinations()
    {
        var viewModel = new ShortcutSettingsViewModel(GlobalHotkeyService.DefaultSettings);
        var first = viewModel.Shortcuts[0];
        viewModel.Shortcuts[1].SetShortcut(first.Modifiers, first.Key);

        var valid = viewModel.TryCreateSettings(out var settings);

        Assert.False(valid);
        Assert.Empty(settings);
        Assert.True(viewModel.HasError);
        Assert.Contains("assigned more than once", viewModel.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreDefaults_ReplacesEditedValues()
    {
        var viewModel = new ShortcutSettingsViewModel(GlobalHotkeyService.DefaultSettings);
        viewModel.Shortcuts[0].SetShortcut(GlobalHotkeyService.ModControl, 0x41);

        viewModel.RestoreDefaults();

        Assert.Equal("Ctrl + Alt + Up Arrow", viewModel.Shortcuts[0].ShortcutText);
    }

    [Fact]
    public void FormatShortcut_UsesHumanKeyNames()
    {
        var setting = new HotkeySettings("IncreaseText", GlobalHotkeyService.ModControl | GlobalHotkeyService.ModAlt, 0x6B);

        Assert.Equal("Ctrl + Alt + Numpad +", GlobalHotkeyService.FormatShortcut(setting));
    }
}
