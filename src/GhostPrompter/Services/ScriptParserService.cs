using GhostPrompter.Models;

namespace GhostPrompter.Services;

/// <summary>Parses the small, format-independent GhostPrompter script syntax.</summary>
public sealed class ScriptParserService
{
    /// <summary>Parses normalized or non-normalized plain text into logical blocks.</summary>
    public PrompterDocument Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var elements = new List<PrompterElement>();
        var blocks = new List<PrompterBlock>();
        var current = new List<PrompterElement>();
        var sourceOffset = 0;

        void CompleteBlock()
        {
            while (current.Count > 0 && string.IsNullOrWhiteSpace(current[0].Text)) current.RemoveAt(0);
            while (current.Count > 0 && string.IsNullOrWhiteSpace(current[^1].Text)) current.RemoveAt(current.Count - 1);
            if (current.Count == 0) return;
            blocks.Add(new PrompterBlock(blocks.Count, current.ToArray()));
            current.Clear();
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed == "---")
            {
                elements.Add(new PrompterElement(PrompterElementKind.Separator, string.Empty, sourceOffset));
                CompleteBlock();
            }
            else
            {
                var kind = PrompterElementKind.Text;
                var value = line;
                if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
                {
                    kind = PrompterElementKind.Title;
                    value = trimmed[1..^1];
                }
                else if (trimmed.StartsWith("//", StringComparison.Ordinal))
                {
                    kind = PrompterElementKind.Comment;
                    value = trimmed[2..].TrimStart();
                }
                var element = new PrompterElement(kind, value, sourceOffset);
                elements.Add(element);
                current.Add(element);
            }
            sourceOffset += line.Length + 1;
        }
        CompleteBlock();
        return new PrompterDocument(elements, blocks, normalized);
    }
}
