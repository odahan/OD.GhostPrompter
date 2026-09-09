using System.Text.Json;
using System.IO;
using GhostPrompter.Models;

namespace GhostPrompter.Services;

/// <summary>Provides versioned, atomic local preference persistence.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
    private readonly string _directory;
    private readonly LoggingService? _logging;
    public SettingsService(string? directory = null, LoggingService? logging = null)
    {
        _directory = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GhostPrompter");
        _logging = logging;
    }
    private string FilePath => Path.Combine(_directory, "settings.json");

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();
            await using var stream = File.OpenRead(FilePath);
            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions);
            if (settings is null || settings.Version != AppSettings.CurrentVersion)
            {
                _logging?.Write("ERROR", "Settings version is unsupported; using safe defaults.");
                return new AppSettings();
            }
            return Validate(settings);
        }
        catch (Exception exception)
        {
            _logging?.Write("ERROR", "Could not load settings; using safe defaults.", exception);
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        var temporary = FilePath + ".tmp";
        await using (var stream = File.Create(temporary)) await JsonSerializer.SerializeAsync(stream, Validate(settings), SerializerOptions);
        File.Move(temporary, FilePath, true);
    }

    private static AppSettings Validate(AppSettings value)
    {
        value.Version = AppSettings.CurrentVersion;
        value.Left = double.IsFinite(value.Left) ? value.Left : 100;
        value.Top = double.IsFinite(value.Top) ? value.Top : 100;
        value.Width = double.IsFinite(value.Width) ? Math.Clamp(value.Width, 200, 4000) : 700;
        value.Height = double.IsFinite(value.Height) ? Math.Clamp(value.Height, 80, 3000) : 250;
        value.BackgroundOpacity = Math.Clamp(value.BackgroundOpacity, 0, 100); value.TextOpacity = Math.Clamp(value.TextOpacity, 20, 100);
        value.FontSize = Math.Clamp(value.FontSize, 16, 72); value.ScrollSpeed = Math.Clamp(value.ScrollSpeed, 10, 300);
        value.StartingHeight = Math.Clamp(value.StartingHeight, 10, 80);
        if (!Enum.IsDefined(value.Mode)) value.Mode = PrompterMode.Blocks;
        return value;
    }
}

/// <summary>Writes one replace-on-launch local application log.</summary>
public sealed class LoggingService : IDisposable
{
    private readonly object _gate = new();
    private StreamWriter? _writer;
    public bool IsAvailable => _writer is not null;

    public void Start()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GhostPrompter", "logs");
            Directory.CreateDirectory(path);
            _writer = new StreamWriter(File.Open(Path.Combine(path, "GhostPrompter.log"), FileMode.Create, FileAccess.Write, FileShare.Read));
        }
        catch { _writer = null; }
    }

    public void Write(string level, string message, Exception? exception = null)
    {
        lock (_gate)
        {
            if (_writer is null) return;
            _writer.WriteLine($"{DateTimeOffset.Now:O} [{level}] {message}{(exception is null ? string.Empty : Environment.NewLine + exception)}");
            if (level is "ERROR" or "FATAL") _writer.Flush();
        }
    }
    public void Dispose() { lock (_gate) { _writer?.Dispose(); _writer = null; } }
}
