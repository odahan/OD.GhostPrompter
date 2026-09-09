using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using GhostPrompter.Services;

namespace GhostPrompter.Tests;

/// <summary>Tests plain-text and Markdown import behavior without a UI thread.</summary>
public sealed class DocumentImportServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"GhostPrompter.Tests.{Guid.NewGuid():N}");
    private readonly DocumentImportService _service = new(new ScriptParserService());

    public DocumentImportServiceTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public async Task LoadAsync_LoadsUtf8TxtThroughTheCommonParser()
    {
        var path = Path.Combine(_directory, "script.txt");
        await File.WriteAllTextAsync(path, "[Start]\r\nBonjour\r\n---\r\n// finish", new UTF8Encoding(false));

        var document = await _service.LoadAsync(path);

        Assert.Equal(2, document.Blocks.Count);
        Assert.Equal("Start", document.Blocks[0].LeadingTitle?.Text);
        Assert.Equal("finish", document.Blocks[1].Elements.Single().Text);
    }

    [Fact]
    public async Task LoadAsync_StripsSupportedMarkdownWhilePreservingReservedSyntax()
    {
        var path = Path.Combine(_directory, "script.md");
        await File.WriteAllTextAsync(path, "# Intro\n**bold** and [link](https://example.test)\n---\n[Title]\n`code`", new UTF8Encoding(false));

        var document = await _service.LoadAsync(path);

        Assert.Equal("Intro", document.Blocks[0].Elements[0].Text);
        Assert.Equal("bold and link", document.Blocks[0].Elements[1].Text);
        Assert.Equal("Title", document.Blocks[1].LeadingTitle?.Text);
        Assert.Equal("code", document.Blocks[1].Elements[1].Text);
    }

    [Fact]
    public async Task LoadAsync_RejectsInvalidUtf8WithoutReplacingCharacters()
    {
        var path = Path.Combine(_directory, "invalid.txt");
        await File.WriteAllBytesAsync(path, [0xC3, 0x28]);

        await Assert.ThrowsAsync<DecoderFallbackException>(() => _service.LoadAsync(path));
    }

    [Fact]
    public async Task LoadAsync_RejectsUtf16Txt()
    {
        var path = Path.Combine(_directory, "utf16.txt");
        await File.WriteAllTextAsync(path, "Not UTF-8", Encoding.Unicode);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() => _service.LoadAsync(path));

        Assert.Equal("TXT files must use UTF-8 encoding.", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_FlattensMarkdownTableCellsAndSkipsAlignmentSyntax()
    {
        var path = Path.Combine(_directory, "table.md");
        await File.WriteAllTextAsync(path, "| Name | Result |\n| --- | :---: |\n| First | Ready |", new UTF8Encoding(false));

        var document = await _service.LoadAsync(path);

        Assert.Equal(["Name Result", "First Ready"], document.Blocks.Single().Elements.Select(element => element.Text));
    }

    [Fact]
    public async Task LoadAsync_ImportsDirectDocxParagraphsAndExcludesTables()
    {
        var path = Path.Combine(_directory, "script.docx");
        using (var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body(
                new Paragraph(new Run(new Text("[Intro]"))),
                new Paragraph(new Run(new Text("First")), new Run(new Break()), new Run(new Text("line")), new Run(new TabChar()), new Run(new Text("next"))),
                new Paragraph(new Run(new Text("---"))),
                new Table(new TableRow(new TableCell(new Paragraph(new Run(new Text("must not import")))))),
                new Paragraph(new Run(new Text("[End]")))));
            mainPart.Document.Save();
        }

        var imported = await _service.LoadAsync(path);

        Assert.Equal(2, imported.Blocks.Count);
        Assert.Equal("Intro", imported.Blocks[0].LeadingTitle?.Text);
        Assert.Equal("End", imported.Blocks[1].LeadingTitle?.Text);
        Assert.Contains(imported.Blocks[0].Elements, element => element.Text.Contains("First", StringComparison.Ordinal));
        Assert.DoesNotContain(imported.Elements, element => element.Text.Contains("must not import", StringComparison.Ordinal));
        Assert.Contains("Tables were ignored during DOCX import.", imported.Warnings!);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
