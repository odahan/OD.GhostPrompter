using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GhostPrompter.Models;

namespace GhostPrompter.Services;

/// <summary>Loads supported files and runs their extracted text through the common parser.</summary>
public interface IDocumentImportService
{
    Task<PrompterDocument> LoadAsync(string path, CancellationToken cancellationToken = default);
}

/// <summary>Implements read-only TXT, Markdown and DOCX imports.</summary>
public sealed class DocumentImportService(ScriptParserService parser) : IDocumentImportService
{
    public const long MaximumSourceBytes = 10_000_000;
    public const int MaximumExtractedCharacters = 5_000_000;

    /// <inheritdoc />
    public Task<PrompterDocument> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() => LoadCoreAsync(path, cancellationToken), cancellationToken);

    private async Task<PrompterDocument> LoadCoreAsync(string path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A source file is required.", nameof(path));
        cancellationToken.ThrowIfCancellationRequested();
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("The selected script does not exist.", path);
        if (info.Length > MaximumSourceBytes) throw new InvalidDataException("The source file exceeds the 10 MB limit.");
        var extension = Path.GetExtension(path);
        var docxExtraction = extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
            ? ExtractDocx(path, cancellationToken)
            : null;
        var text = extension.ToLowerInvariant() switch
        {
            ".txt" => await ReadUtf8Async(path, cancellationToken),
            ".md" or ".markdown" => MarkdownToText(await ReadUtf8Async(path, cancellationToken), cancellationToken),
            ".docx" => docxExtraction!.Text,
            _ => throw new NotSupportedException("Only TXT, Markdown and DOCX files are supported."),
        };
        EnsureExtractedLength(text.Length);
        cancellationToken.ThrowIfCancellationRequested();
        var parsed = parser.Parse(text, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return docxExtraction?.HasIgnoredTables == true
            ? new PrompterDocument(parsed.Elements, parsed.Blocks, parsed.SourceText, ["Tables were ignored during DOCX import."])
            : parsed;
    }

    private static async Task<string> ReadUtf8Async(string path, CancellationToken cancellationToken)
    {
        var encoding = new UTF8Encoding(false, true);
        await using var stream = File.OpenRead(path);
        var prefix = new byte[4];
        var prefixLength = await stream.ReadAsync(prefix, cancellationToken);
        stream.Position = 0;
        if ((prefixLength >= 2 && ((prefix[0] == 0xFF && prefix[1] == 0xFE) || (prefix[0] == 0xFE && prefix[1] == 0xFF)))
            || (prefixLength == 4 && ((prefix[0] == 0x00 && prefix[1] == 0x00 && prefix[2] == 0xFE && prefix[3] == 0xFF) || (prefix[0] == 0xFF && prefix[1] == 0xFE && prefix[2] == 0x00 && prefix[3] == 0x00))))
        {
            throw new InvalidDataException("TXT files must use UTF-8 encoding.");
        }
        using var reader = new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: true);
        var builder = new StringBuilder();
        var buffer = new char[8192];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (builder.Length + read > MaximumExtractedCharacters) throw new InvalidDataException("The extracted text exceeds the 10 MB limit.");
            builder.Append(buffer, 0, read);
        }
        return builder.ToString().Replace("\r\n", "\n").Replace('\r', '\n');
    }

    private static DocxExtraction ExtractDocx(string path, CancellationToken cancellationToken)
    {
        using var word = WordprocessingDocument.Open(path, false);
        var mainPart = word.MainDocumentPart ?? throw new InvalidDataException("The DOCX document has no main part.");
        var body = mainPart.Document?.Body ?? throw new InvalidDataException("The DOCX document has no main body.");
        var builder = new StringBuilder();
        foreach (var paragraph in body.Elements<Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var run in paragraph.Descendants<Run>().Where(run => !run.Ancestors().Any(IsExcludedDocxContainer)))
            {
                if (run.Ancestors<DeletedRun>().Any()) continue;
                foreach (var child in run.ChildElements)
                {
                    if (child is DeletedText) continue;
                    if (child is Text text) AppendLimited(builder, text.Text);
                    else if (child is TabChar) AppendLimited(builder, " ");
                    else if (child is Break or CarriageReturn) AppendLimited(builder, "\n");
                }
            }
            AppendLimited(builder, "\n");
        }
        return new DocxExtraction(builder.ToString().Replace("\r\n", "\n").Replace('\r', '\n'), body.Elements<Table>().Any());
    }

    private static void AppendLimited(StringBuilder builder, string value)
    {
        if (builder.Length + value.Length > MaximumExtractedCharacters) throw new InvalidDataException("The extracted text exceeds the 10 MB limit.");
        builder.Append(value);
    }

    private static void EnsureExtractedLength(int length)
    {
        if (length > MaximumExtractedCharacters) throw new InvalidDataException("The extracted text exceeds the 10 MB limit.");
    }

    /// <summary>Converts the supported Markdown subset to readable plain text without evaluating active content.</summary>
    internal static string MarkdownToText(string markdown, CancellationToken cancellationToken = default)
    {
        var text = Regex.Replace(markdown, "(?is)<(script|style)\\b[^>]*>.*?</\\1>", string.Empty);
        var output = new List<string>();
        string? fence = null;
        foreach (var sourceLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fenceMatch = Regex.Match(sourceLine, "^\\s{0,3}(`{3,}|~{3,})");
            if (fenceMatch.Success)
            {
                var marker = fenceMatch.Groups[1].Value;
                if (fence is null) fence = marker;
                else if (marker[0] == fence[0] && marker.Length >= fence.Length) fence = null;
                continue;
            }
            if (fence is not null)
            {
                output.Add(sourceLine);
                continue;
            }
            var line = sourceLine;
            var reserved = line.Trim() is "---" || (line.Trim().Length > 2 && line.Trim()[0] == '[' && line.Trim()[^1] == ']');
            if (!reserved && Regex.IsMatch(line, "^\\s*([-*_])(?:\\s*\\1){2,}\\s*$")) { output.Add(string.Empty); continue; }
            if (!reserved) line = Regex.Replace(line, "^\\s{0,3}#{1,6}\\s+", "");
            line = Regex.Replace(line, "^\\s{0,3}>\\s?", "");
            line = Regex.Replace(line, "^\\s*(?:[-+*]|\\d+[.)])\\s+", "");
            line = StripMarkdownInline(line);
            if (Regex.IsMatch(line, "^\\s*\\|?\\s*:?-{3,}:?\\s*(\\|\\s*:?-{3,}:?\\s*)+\\|?\\s*$")) continue;
            if (!reserved && Regex.Matches(line, "\\|").Count >= 2)
            {
                line = string.Join(" ", line.Trim().Trim('|').Split('|').Select(cell => cell.Trim()).Where(cell => cell.Length > 0));
            }
            output.Add(line.TrimEnd());
        }
        return string.Join("\n", output);
    }

    private static string StripMarkdownInline(string line)
    {
        var codeSpans = new List<string>();
        line = Regex.Replace(line, "(`+)(.+?)\\1", match =>
        {
            codeSpans.Add(match.Groups[2].Value);
            return $"\uE000{codeSpans.Count - 1}\uE001";
        });
        line = Regex.Replace(line, "!\\[([^]]*)\\]\\([^)]*\\)", "$1");
        line = Regex.Replace(line, "\\[([^]]+)\\]\\([^)]*\\)", "$1");
        line = Regex.Replace(line, "<(https?://[^>]+)>", "$1", RegexOptions.IgnoreCase);
        line = Regex.Replace(line,
            "</?(?:a|abbr|b|blockquote|br|code|div|em|h[1-6]|hr|i|img|li|ol|p|pre|span|strong|table|tbody|td|th|thead|tr|ul)\\b[^>]*>",
            string.Empty,
            RegexOptions.IgnoreCase);
        line = Regex.Replace(line, "(?<!\\*)\\*{1,3}(?=\\S)([^*]*?\\S)\\*{1,3}(?!\\*)", "$1");
        line = Regex.Replace(line, "(?<![\\w_])_{1,3}(?=\\S)([^_]*?\\S)_{1,3}(?![\\w_])", "$1");
        for (var index = 0; index < codeSpans.Count; index++) line = line.Replace($"\uE000{index}\uE001", codeSpans[index], StringComparison.Ordinal);
        return line;
    }

    private static bool IsExcludedDocxContainer(DocumentFormat.OpenXml.OpenXmlElement element) =>
        element is Drawing || element.LocalName is "txbxContent" or "textbox";

    private sealed record DocxExtraction(string Text, bool HasIgnoredTables);
}
