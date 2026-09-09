using GhostPrompter.Models;
using GhostPrompter.Services;

namespace GhostPrompter.Tests;

/// <summary>Tests page partitioning independently from the WPF measurement adapter.</summary>
public sealed class PaginationServiceTests
{
    [Fact]
    public void Paginate_KeepsEveryElementInOrderAcrossPages()
    {
        var block = new PrompterBlock(0, Enumerable.Range(0, 5).Select(i => new PrompterElement(PrompterElementKind.Text, $"Line {i}", i)).ToArray());
        var pages = new PaginationService().Paginate(block, 2, elements => elements.Count);

        Assert.Equal(3, pages.Count);
        Assert.Collection(pages, page => { Assert.Equal(0, page.StartElement); Assert.Equal(1, page.EndElement); },
            page => { Assert.Equal(2, page.StartElement); Assert.Equal(3, page.EndElement); },
            page => { Assert.Equal(4, page.StartElement); Assert.Equal(4, page.EndElement); });
    }

    [Fact]
    public void Paginate_SplitsAnOverHeightSingleParagraphWithoutLosingUnicode()
    {
        const string text = "alpha beta 😀 gamma delta";
        var block = new PrompterBlock(0, [new(PrompterElementKind.Text, text, 0)]);
        var pages = new PaginationService().Paginate(block, 7, elements => elements.Sum(element => element.Text.Length));

        Assert.True(pages.Count > 1);
        Assert.Equal(text, string.Concat(pages.SelectMany(page => page.Elements).Select(element => element.Text)));
        Assert.DoesNotContain(pages.SelectMany(page => page.Elements), element => element.Text.EndsWith('\uD83D'));
    }

    [Fact]
    public void Paginate_RepeatsTheLeadingTitleOnlyWhenItFits()
    {
        var title = new PrompterElement(PrompterElementKind.Title, "Title", 0);
        var block = new PrompterBlock(0, [title, new(PrompterElementKind.Text, "One", 1), new(PrompterElementKind.Text, "Two", 2), new(PrompterElementKind.Text, "Three", 3)]);
        var pages = new PaginationService().Paginate(block, 2, elements => elements.Count);

        Assert.Equal(2, pages.Count);
        Assert.False(pages[1].RepeatsLeadingTitle);

        pages = new PaginationService().Paginate(block, 3, elements => elements.Count);
        Assert.True(pages[1].RepeatsLeadingTitle);
        Assert.Equal("Title", pages[1].Elements[0].Text);
    }
}
