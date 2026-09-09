using GhostPrompter.Models;

namespace GhostPrompter.Services;

/// <summary>Provides real-renderer-backed block page partitioning.</summary>
public sealed class PaginationService
{
    /// <summary>Splits one block into fitting element ranges, using a caller-supplied measurement function.</summary>
    public IReadOnlyList<PrompterPage> Paginate(PrompterBlock block, double availableHeight, Func<IReadOnlyList<PrompterElement>, double> measure)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(availableHeight);
        var layoutElements = ExpandOverflowingElements(block.Elements, availableHeight, measure);
        var pages = new List<PrompterPage>();
        var index = 0;
        while (index < layoutElements.Count)
        {
            var end = index;
            while (end < layoutElements.Count && measure(layoutElements.Skip(index).Take(end - index + 1).ToArray()) <= availableHeight) end++;
            if (end == index) end++; // A single over-height element must still remain reachable.
            var pageElements = layoutElements.Skip(index).Take(end - index).ToArray();
            var repeatsLeadingTitle = false;
            if (pages.Count > 0 && block.LeadingTitle is { } leadingTitle)
            {
                var withTitle = new[] { leadingTitle }.Concat(pageElements).ToArray();
                if (measure(withTitle) <= availableHeight)
                {
                    pageElements = withTitle;
                    repeatsLeadingTitle = true;
                }
            }
            pages.Add(new PrompterPage(block.Index, pages.Count, index, end - 1,
                new TextPosition(index, 0), repeatsLeadingTitle, pageElements));
            index = end;
        }
        return pages;
    }

    private static IReadOnlyList<PrompterElement> ExpandOverflowingElements(
        IReadOnlyList<PrompterElement> elements,
        double availableHeight,
        Func<IReadOnlyList<PrompterElement>, double> measure)
    {
        var expanded = new List<PrompterElement>();
        foreach (var element in elements)
        {
            if (measure([element]) <= availableHeight || string.IsNullOrEmpty(element.Text))
            {
                expanded.Add(element);
                continue;
            }
            expanded.AddRange(SplitElement(element, availableHeight, measure));
        }
        return expanded;
    }

    private static IEnumerable<PrompterElement> SplitElement(
        PrompterElement element,
        double availableHeight,
        Func<IReadOnlyList<PrompterElement>, double> measure)
    {
        var offset = 0;
        while (offset < element.Text.Length)
        {
            var remainingLength = element.Text.Length - offset;
            var length = FindLargestFittingLength(element, offset, remainingLength, availableHeight, measure);
            if (length == 0) length = Math.Min(remainingLength, 1);
            var preferredBoundary = FindPreferredBoundary(element.Text, offset, length);
            if (preferredBoundary > 0) length = preferredBoundary;
            length = AvoidSplittingSurrogatePair(element.Text, offset, length);
            if (length <= 0) length = Math.Min(remainingLength, 1);
            yield return new PrompterElement(element.Kind, element.Text.Substring(offset, length), element.SourceOffset + offset);
            offset += length;
        }
    }

    private static int FindLargestFittingLength(PrompterElement element, int offset, int maximumLength, double availableHeight, Func<IReadOnlyList<PrompterElement>, double> measure)
    {
        var low = 0;
        var high = maximumLength;
        while (low < high)
        {
            var middle = low + (high - low + 1) / 2;
            var candidate = new PrompterElement(element.Kind, element.Text.Substring(offset, middle), element.SourceOffset + offset);
            if (measure([candidate]) <= availableHeight) low = middle;
            else high = middle - 1;
        }
        return low;
    }

    private static int FindPreferredBoundary(string text, int offset, int length)
    {
        for (var index = length - 1; index > 0; index--)
        {
            if (char.IsWhiteSpace(text[offset + index])) return index + 1;
        }
        return length;
    }

    private static int AvoidSplittingSurrogatePair(string text, int offset, int length)
    {
        if (offset + length < text.Length && length > 0 && char.IsHighSurrogate(text[offset + length - 1]) && char.IsLowSurrogate(text[offset + length])) return length - 1;
        return length;
    }
}
