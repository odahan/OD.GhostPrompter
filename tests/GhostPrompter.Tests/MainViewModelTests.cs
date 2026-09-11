using GhostPrompter.Models;
using GhostPrompter.Services;
using GhostPrompter.ViewModels;

namespace GhostPrompter.Tests;

/// <summary>Tests stale asynchronous import protection without a window.</summary>
public sealed class MainViewModelTests
{
    [Fact]
    public async Task EditText_IsAvailableOnlyAfterARealDocumentLoad()
    {
        var parser = new ScriptParserService();
        var viewModel = new MainViewModel(new ImmediateImporter(parser.Parse("Correct this text.")), parser, new PrompterViewModel());
        var requested = false;
        viewModel.TextEditorRequested += (_, _) => requested = true;

        Assert.False(viewModel.EditTextCommand.CanExecute(null));

        await viewModel.LoadPathAsync("script.txt");
        viewModel.EditTextCommand.Execute(null);

        Assert.True(viewModel.EditTextCommand.CanExecute(null));
        Assert.Equal("Correct this text.", viewModel.EditableText);
        Assert.True(requested);
    }

    [Fact]
    public async Task EditText_AllowsCorrectionOfALoadedEmptyDocument()
    {
        var parser = new ScriptParserService();
        var viewModel = new MainViewModel(new ImmediateImporter(PrompterDocument.Empty), parser, new PrompterViewModel());

        await viewModel.LoadPathAsync("empty.txt");

        Assert.True(viewModel.EditTextCommand.CanExecute(null));
        Assert.Equal(string.Empty, viewModel.EditableText);
    }

    [Fact]
    public async Task LoadPathAsync_OnlyAppliesTheMostRecentCompletedRequest()
    {
        var importer = new DelayedImporter();
        var viewModel = new MainViewModel(importer, new ScriptParserService(), new PrompterViewModel());

        var first = viewModel.LoadPathAsync("first.txt");
        var second = viewModel.LoadPathAsync("second.txt");
        importer.Complete("first.txt", new ScriptParserService().Parse("First"));
        importer.Complete("second.txt", new ScriptParserService().Parse("Second"));
        await Task.WhenAll(first, second);

        Assert.Equal("second.txt", viewModel.CurrentFile);
        Assert.Equal("Script loaded.", viewModel.Status);
    }

    [Fact]
    public async Task ShowTitlesOff_DisablesADocumentContainingOnlyTitles()
    {
        var parser = new ScriptParserService();
        var importer = new ImmediateImporter(parser.Parse("[Only title]"));
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(importer, parser, prompter);

        await viewModel.LoadPathAsync("title-only.txt");
        viewModel.ShowTitles = false;

        Assert.Empty(prompter.RenderElements);
        Assert.Equal("No visible content.", viewModel.Status);
    }

    [Fact]
    public async Task ScrollMode_PreservesSeparatorsAsVisualSpacingElements()
    {
        var parser = new ScriptParserService();
        var importer = new ImmediateImporter(parser.Parse("First\n---\nSecond"));
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(importer, parser, prompter) { Mode = PrompterMode.Scroll };

        await viewModel.LoadPathAsync("scroll.txt");

        Assert.Contains(prompter.RenderElements, element => element.Kind == PrompterElementKind.Separator);
    }

    [Fact]
    public async Task BlocksNavigation_StopsAtDocumentBoundaries()
    {
        var parser = new ScriptParserService();
        var importer = new ImmediateImporter(parser.Parse("First\n---\nSecond"));
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(importer, parser, prompter);
        await viewModel.LoadPathAsync("blocks.txt");

        viewModel.NextPageCommand.Execute(null);
        Assert.Equal("Second", prompter.Content);

        viewModel.NextPageCommand.Execute(null);
        Assert.Equal("Second", prompter.Content);

        viewModel.PreviousPageCommand.Execute(null);
        Assert.Equal("First", prompter.Content);

        viewModel.PreviousPageCommand.Execute(null);
        Assert.Equal("First", prompter.Content);
    }

    [Fact]
    public async Task ScrollPlayback_UsesMeasuredLayoutAndRestartStaysPaused()
    {
        var parser = new ScriptParserService();
        var importer = new ImmediateImporter(parser.Parse("A sufficiently long scrolling script."));
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(importer, parser, prompter) { Mode = PrompterMode.Scroll };
        await viewModel.LoadPathAsync("scroll.txt");
        viewModel.ConfigureScrollLayout(maximumOffset: 100);

        viewModel.TogglePlayPauseCommand.Execute(null);
        viewModel.AdvanceScroll(TimeSpan.FromSeconds(1));
        Assert.True(viewModel.IsScrollRunning);
        Assert.Equal(50, prompter.ScrollOffset);

        viewModel.RestartCommand.Execute(null);
        Assert.False(viewModel.IsScrollRunning);
        Assert.Equal(0, prompter.ScrollOffset);
    }

    [Fact]
    public async Task HidingThePrompter_PausesScrollWithoutResettingItsPosition()
    {
        var parser = new ScriptParserService();
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(new ImmediateImporter(parser.Parse("Scrolling content")), parser, prompter) { Mode = PrompterMode.Scroll };
        await viewModel.LoadPathAsync("scroll.txt");
        viewModel.ConfigureScrollLayout(100);
        viewModel.TogglePlayPauseCommand.Execute(null);
        viewModel.AdvanceScroll(TimeSpan.FromSeconds(1));

        viewModel.ToggleVisibilityCommand.Execute(null);

        Assert.False(viewModel.IsVisible);
        Assert.False(viewModel.IsScrollRunning);
        Assert.Equal(50, prompter.ScrollOffset);
    }

    [Fact]
    public async Task ScrollLayoutRefresh_PreservesTheCurrentOffset()
    {
        var parser = new ScriptParserService();
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(new ImmediateImporter(parser.Parse("Scrolling content")), parser, prompter) { Mode = PrompterMode.Scroll };
        await viewModel.LoadPathAsync("scroll.txt");
        viewModel.ConfigureScrollLayout(200);
        viewModel.TogglePlayPauseCommand.Execute(null);
        viewModel.AdvanceScroll(TimeSpan.FromSeconds(1));

        viewModel.ShowProgress = false;
        viewModel.ConfigureScrollLayout(200);

        Assert.Equal(50, prompter.ScrollOffset);
        Assert.False(viewModel.IsScrollRunning);

        viewModel.StartingHeight = 60;

        Assert.Equal(50, prompter.ScrollOffset);
        Assert.False(viewModel.IsScrollRunning);
    }

    [Fact]
    public async Task ScrollBackOnePage_MovesByTheViewportWithoutChangingPlaybackState()
    {
        var parser = new ScriptParserService();
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(new ImmediateImporter(parser.Parse("Scrolling content")), parser, prompter) { Mode = PrompterMode.Scroll };
        await viewModel.LoadPathAsync("scroll.txt");
        viewModel.ConfigureScrollLayout(maximumOffset: 1000, viewportHeight: 200);
        viewModel.TogglePlayPauseCommand.Execute(null);
        viewModel.AdvanceScroll(TimeSpan.FromSeconds(10));

        viewModel.HandleHotkey("ScrollBackOnePage");

        Assert.Equal(300, prompter.ScrollOffset);
        Assert.True(viewModel.IsScrollRunning);

        viewModel.TogglePlayPauseCommand.Execute(null);
        viewModel.HandleHotkey("ScrollBackOnePage");

        Assert.Equal(100, prompter.ScrollOffset);
        Assert.False(viewModel.IsScrollRunning);
    }

    [Fact]
    public async Task BlocksReflow_PreservesTheCurrentSourcePassage()
    {
        var parser = new ScriptParserService();
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(new ImmediateImporter(parser.Parse("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMN")), parser, prompter);
        await viewModel.LoadPathAsync("blocks.txt");
        static double Measure(IReadOnlyList<PrompterElement> elements) => elements.Sum(element => element.Text.Length);
        viewModel.ReflowBlocks(10, Measure);
        viewModel.NextPageCommand.Execute(null);
        viewModel.NextPageCommand.Execute(null);
        var sourceOffset = prompter.RenderElements[0].SourceOffset;

        viewModel.ReflowBlocks(5, Measure);

        var first = prompter.RenderElements[0];
        Assert.InRange(sourceOffset, first.SourceOffset, first.SourceOffset + first.Text.Length - 1);
    }

    [Fact]
    public async Task LoadingAnEmptyScript_DisablesScrollPlaybackAndClearsTheOffset()
    {
        var parser = new ScriptParserService();
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(new DocumentSequenceImporter(parser.Parse("Scrolling content"), parser.Parse("---")), parser, prompter) { Mode = PrompterMode.Scroll };
        await viewModel.LoadPathAsync("content.txt");
        viewModel.ConfigureScrollLayout(200);
        viewModel.TogglePlayPauseCommand.Execute(null);
        viewModel.AdvanceScroll(TimeSpan.FromSeconds(1));

        await viewModel.LoadPathAsync("empty.txt");
        viewModel.TogglePlayPauseCommand.Execute(null);

        Assert.False(viewModel.IsScrollRunning);
        Assert.Equal(0, prompter.ScrollOffset);
        Assert.Equal(0, viewModel.Progress);
    }

    [Fact]
    public async Task FailedImport_PreservesTheCurrentDocumentAndPath()
    {
        var parser = new ScriptParserService();
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(new SequencedImporter(parser.Parse("Retained")), parser, prompter);
        await viewModel.LoadPathAsync("good.txt");

        await viewModel.LoadPathAsync("broken.txt");

        Assert.Equal("good.txt", viewModel.CurrentFile);
        Assert.Equal("Retained", prompter.Content);
        Assert.StartsWith("Could not load script:", viewModel.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Restart_ReturnsBlocksModeToTheFirstBlock()
    {
        var parser = new ScriptParserService();
        var prompter = new PrompterViewModel();
        var viewModel = new MainViewModel(new ImmediateImporter(parser.Parse("First\n---\nSecond")), parser, prompter);
        await viewModel.LoadPathAsync("blocks.txt");
        viewModel.NextPageCommand.Execute(null);

        viewModel.RestartCommand.Execute(null);

        Assert.Equal("First", prompter.Content);
    }

    [Fact]
    public async Task HiddenPrompter_CannotStartScrollPlayback()
    {
        var parser = new ScriptParserService();
        var viewModel = new MainViewModel(new ImmediateImporter(parser.Parse("Text")), parser, new PrompterViewModel()) { Mode = PrompterMode.Scroll };
        await viewModel.LoadPathAsync("scroll.txt");
        viewModel.ConfigureScrollLayout(100);
        viewModel.ToggleVisibilityCommand.Execute(null);

        viewModel.TogglePlayPauseCommand.Execute(null);

        Assert.False(viewModel.IsScrollRunning);
    }

    [Fact]
    public void ToggleClickThrough_UpdatesThePreferenceAndVisibleStatus()
    {
        var parser = new ScriptParserService();
        var viewModel = new MainViewModel(new ImmediateImporter(PrompterDocument.Empty), parser, new PrompterViewModel());

        viewModel.ToggleClickThroughCommand.Execute(null);

        Assert.True(viewModel.ClickThroughPreferred);
        Assert.Contains("on", viewModel.PresentationStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TogglePresentation_UpdatesTheModeAndPausesPlayback()
    {
        var parser = new ScriptParserService();
        var viewModel = new MainViewModel(new ImmediateImporter(PrompterDocument.Empty), parser, new PrompterViewModel());

        viewModel.TogglePresentationCommand.Execute(null);

        Assert.True(viewModel.IsPresentation);
        Assert.StartsWith("Presentation", viewModel.PresentationStatus, StringComparison.Ordinal);
        Assert.False(viewModel.IsScrollRunning);
    }

    [Fact]
    public async Task SupersededCancellation_DoesNotEscapeFromLoadPathAsync()
    {
        var parser = new ScriptParserService();
        var importer = new CancellationAwareImporter(parser.Parse("Latest"));
        var viewModel = new MainViewModel(importer, parser, new PrompterViewModel());

        var first = viewModel.LoadPathAsync("first.txt");
        var second = viewModel.LoadPathAsync("second.txt");
        await Task.WhenAll(first, second);

        Assert.Equal("second.txt", viewModel.CurrentFile);
        Assert.Equal("Latest", viewModel.Prompter.Content);
    }

    [Fact]
    public async Task HiddenTitleAndBlankLines_ReportNoVisibleContent()
    {
        var parser = new ScriptParserService();
        var viewModel = new MainViewModel(new ImmediateImporter(parser.Parse("[Only title]\n")), parser, new PrompterViewModel())
        {
            Mode = PrompterMode.Scroll,
        };
        await viewModel.LoadPathAsync("title.txt");

        viewModel.ShowTitles = false;

        Assert.Equal("No visible content.", viewModel.Status);
        Assert.Empty(viewModel.Prompter.RenderElements);
    }

    private sealed class DelayedImporter : IDocumentImportService
    {
        private readonly Dictionary<string, TaskCompletionSource<PrompterDocument>> _requests = [];
        public Task<PrompterDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            var source = new TaskCompletionSource<PrompterDocument>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(path, source);
            return source.Task;
        }
        public void Complete(string path, PrompterDocument document) => _requests[path].SetResult(document);
    }

    private sealed class ImmediateImporter(PrompterDocument document) : IDocumentImportService
    {
        public Task<PrompterDocument> LoadAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(document);
    }

    private sealed class SequencedImporter(PrompterDocument firstDocument) : IDocumentImportService
    {
        private bool _used;
        public Task<PrompterDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!_used) { _used = true; return Task.FromResult(firstDocument); }
            throw new InvalidDataException("The source is corrupt.");
        }
    }

    private sealed class DocumentSequenceImporter(params PrompterDocument[] documents) : IDocumentImportService
    {
        private int _index;
        public Task<PrompterDocument> LoadAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(documents[_index++]);
    }

    private sealed class CancellationAwareImporter(PrompterDocument latest) : IDocumentImportService
    {
        private int _requestCount;

        public async Task<PrompterDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _requestCount) == 1)
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            return latest;
        }
    }
}
