using System.Text;
using GhostPrompter.Services;

namespace GhostPrompter.Tests;

/// <summary>Verifies the text editor's TXT-only persistence contract.</summary>
public sealed class TextFileSaveServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"GhostPrompter.Editor.{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveAsync_WritesUtf8WithoutByteOrderMark()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "corrected.txt");

        await new TextFileSaveService().SaveAsync(path, "Été — corrected");

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Equal("Été — corrected", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task SaveAsync_RejectsEveryExtensionExceptTxt()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "corrected.md");

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => new TextFileSaveService().SaveAsync(path, "Text"));

        Assert.Contains("TXT", exception.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }
}
