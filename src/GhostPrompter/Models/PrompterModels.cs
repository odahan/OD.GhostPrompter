namespace GhostPrompter.Models;

/// <summary>Defines the two available presentation modes.</summary>
public enum PrompterMode { Blocks, Scroll }

/// <summary>Defines an element interpreted by the GhostPrompter script parser.</summary>
public enum PrompterElementKind { Text, Title, Comment, Separator }

/// <summary>Represents a source position that survives visual reflow.</summary>
public readonly record struct TextPosition(int ElementIndex, int CharacterOffset);

/// <summary>Represents one parsed script line.</summary>
public sealed record PrompterElement(PrompterElementKind Kind, string Text, int SourceOffset);

/// <summary>Represents a logical block delimited by separators.</summary>
public sealed record PrompterBlock(int Index, IReadOnlyList<PrompterElement> Elements)
{
    public PrompterElement? LeadingTitle => Elements.FirstOrDefault() is { Kind: PrompterElementKind.Title } title ? title : null;
}

/// <summary>Represents the parsed script independently of its file format.</summary>
public sealed record PrompterDocument(
    IReadOnlyList<PrompterElement> Elements,
    IReadOnlyList<PrompterBlock> Blocks,
    string SourceText,
    IReadOnlyList<string>? Warnings = null)
{
    public static PrompterDocument Empty { get; } = new([], [], string.Empty);
    public bool IsEmpty => Blocks.Count == 0;
}

/// <summary>Represents a page as references to a contiguous portion of a block.</summary>
public sealed record PrompterPage(
    int BlockIndex,
    int PageIndex,
    int StartElement,
    int EndElement,
    TextPosition StartPosition,
    bool RepeatsLeadingTitle,
    IReadOnlyList<PrompterElement> Elements);

/// <summary>Stores a global shortcut's persisted definition.</summary>
public sealed record HotkeySettings(string Action, uint Modifiers, uint Key);

/// <summary>Stores persisted user preferences.</summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 700;
    public double Height { get; set; } = 250;
    public double BackgroundOpacity { get; set; } = 75;
    public double TextOpacity { get; set; } = 75;
    public double FontSize { get; set; } = 30;
    public PrompterMode Mode { get; set; } = PrompterMode.Blocks;
    public bool ShowTitles { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public bool ClickThroughPreferred { get; set; }
    public double ScrollSpeed { get; set; } = 50;
    public double StartingHeight { get; set; } = 50;
    public string? LastFilePath { get; set; }
    public List<HotkeySettings> Hotkeys { get; set; } = [];
}

/// <summary>Reports the Windows capture-exclusion state for one window.</summary>
public enum CaptureExclusionState { NotInitialized, Active, Unavailable, Failed }

/// <summary>Contains the outcome of applying capture exclusion.</summary>
public sealed record CaptureExclusionResult(CaptureExclusionState State, uint? Affinity, int? Error)
{
    public static CaptureExclusionResult NotInitialized { get; } = new(CaptureExclusionState.NotInitialized, null, null);
}
