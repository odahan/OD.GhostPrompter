using GhostPrompter.Models;
using GhostPrompter.Services;

namespace GhostPrompter.Tests;

/// <summary>Tests the common syntax shared by every import format.</summary>
public sealed class ScriptParserServiceTests
{
    private readonly ScriptParserService _parser = new();

    [Fact]
    public void Parse_RecognizesTitlesCommentsAndSeparators()
    {
        var document = _parser.Parse("[Introduction]\n// reminder\nBody\n---\n[End]");

        Assert.Equal(2, document.Blocks.Count);
        Assert.Equal(PrompterElementKind.Title, document.Blocks[0].Elements[0].Kind);
        Assert.Equal("Introduction", document.Blocks[0].Elements[0].Text);
        Assert.Equal(PrompterElementKind.Comment, document.Blocks[0].Elements[1].Kind);
        Assert.Equal("reminder", document.Blocks[0].Elements[1].Text);
        Assert.Equal("End", document.Blocks[1].LeadingTitle?.Text);
    }

    [Fact]
    public void Parse_IgnoresEmptyBlocksAndTrimsOnlyBlockEdges()
    {
        var document = _parser.Parse("---\n\n\nFirst\n\n\n---\n---\n\nSecond\n\n---");

        Assert.Equal(2, document.Blocks.Count);
        Assert.Equal("First", document.Blocks[0].Elements.Single().Text);
        Assert.Equal("Second", document.Blocks[1].Elements.Single().Text);
    }

    [Theory]
    [InlineData("[]", PrompterElementKind.Text)]
    [InlineData("A [title] in a sentence", PrompterElementKind.Text)]
    [InlineData(" [A title] ", PrompterElementKind.Title)]
    public void Parse_OnlyRecognizesReservedWholeLines(string input, PrompterElementKind expectedKind)
    {
        var document = _parser.Parse(input);

        Assert.Equal(expectedKind, document.Blocks.Single().Elements.Single().Kind);
    }

    [Fact]
    public void Parse_NormalizesLineEndingsAndPreservesUnicode()
    {
        var document = _parser.Parse("[Été]\r\nRésumé 日本語\r// vérifier");

        Assert.Equal("Été", document.Blocks.Single().Elements[0].Text);
        Assert.Equal("Résumé 日本語", document.Blocks.Single().Elements[1].Text);
        Assert.Equal("vérifier", document.Blocks.Single().Elements[2].Text);
        Assert.DoesNotContain('\r', document.SourceText);
    }
}
