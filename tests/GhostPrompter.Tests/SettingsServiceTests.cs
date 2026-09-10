using GhostPrompter.Models;
using GhostPrompter.Services;

namespace GhostPrompter.Tests;

/// <summary>Tests resilient, bounded local settings persistence.</summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"GhostPrompter.Settings.{Guid.NewGuid():N}");

    [Fact]
    public async Task LoadAsync_InvalidJsonReturnsSafeDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), "not json");

        var loaded = await new SettingsService(_directory).LoadAsync();

        Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
        Assert.Equal(700, loaded.Width);
        Assert.Equal(250, loaded.Height);
    }

    [Fact]
    public async Task SaveAndLoadAsync_ClampsInvalidPersistedValues()
    {
        var service = new SettingsService(_directory);
        await service.SaveAsync(new AppSettings { Width = 99_999, Height = -5, FontSize = 1, TextOpacity = 0, StartingHeight = 99, ScrollSpeed = -20 });

        var loaded = await service.LoadAsync();

        Assert.Equal(4000, loaded.Width);
        Assert.Equal(80, loaded.Height);
        Assert.Equal(16, loaded.FontSize);
        Assert.Equal(20, loaded.TextOpacity);
        Assert.Equal(80, loaded.StartingHeight);
        Assert.Equal(2, loaded.ScrollSpeed);
    }

    [Fact]
    public async Task LoadAsync_UnsupportedVersionReturnsSafeDefaults()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), "{\"version\":99,\"width\":1200}");

        var loaded = await new SettingsService(_directory).LoadAsync();

        Assert.Equal(AppSettings.CurrentVersion, loaded.Version);
        Assert.Equal(700, loaded.Width);
    }

    [Fact]
    public async Task LoadAsync_InvalidModeFallsBackToBlocks()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "settings.json"), "{\"version\":1,\"mode\":99}");

        var loaded = await new SettingsService(_directory).LoadAsync();

        Assert.Equal(PrompterMode.Blocks, loaded.Mode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
