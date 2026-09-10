using System.IO;
using System.Text;

namespace GhostPrompter.Services;

/// <summary>Saves edited prompter text as a UTF-8 TXT file.</summary>
public sealed class TextFileSaveService
{
    /// <summary>Writes text to a file after validating the TXT-only editor contract.</summary>
    public async Task SaveAsync(string path, string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A destination file is required.", nameof(path));
        if (!Path.GetExtension(path).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Edited scripts can only be saved as TXT files.");
        if (text.Length > DocumentImportService.MaximumExtractedCharacters)
            throw new InvalidDataException("The edited text exceeds the 10 MB limit.");

        await File.WriteAllTextAsync(path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken);
    }
}
