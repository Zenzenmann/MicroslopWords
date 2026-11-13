using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MicroslopWords.App.Controls;
using MicroslopWords.App.Services;
using MicroslopWords.App.ViewModels;
using MicroslopWords.App.Views;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class FindUiTests
{
    [AvaloniaFact]
    public void Find_panel_opens_and_focuses_the_search_box()
    {
        using var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        viewModel.FindCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var panel = window
            .GetVisualDescendants()
            .OfType<FindPanel>()
            .Single();
        var searchBox = panel.FindControl<TextBox>("SearchBox");

        panel.IsVisible.ShouldBeTrue();
        searchBox.ShouldNotBeNull();
        searchBox.IsFocused.ShouldBeTrue();

        window.Close();
    }

    [Fact]
    public void Find_next_moves_through_matches_and_wraps()
    {
        using var viewModel = new MainViewModel();
        viewModel.InsertText("Hello world Hello");
        viewModel.Selection = Selection.StartOf(viewModel.Document);

        viewModel.FindCommand.Execute(null);
        viewModel.FindQuery = "Hello";

        viewModel.FindMatchCount.ShouldBe(2);
        viewModel.HasSearchMatches.ShouldBeTrue();
        viewModel.FindStatusText.ShouldBe("2 matches.");
        viewModel.SearchHighlights.Count.ShouldBe(2);

        viewModel.FindNextCommand.Execute(null);
        viewModel.FindStatusText.ShouldBe("Match 1 of 2.");
        viewModel.Selection.IsCollapsed.ShouldBeFalse();

        viewModel.FindNextCommand.Execute(null);
        viewModel.FindStatusText.ShouldBe("Match 2 of 2.");

        viewModel.FindNextCommand.Execute(null);
        viewModel.FindStatusText.ShouldBe("Match 1 of 2. Wrapped to the beginning.");

        viewModel.FindPreviousCommand.Execute(null);
        viewModel.FindStatusText.ShouldBe("Match 2 of 2. Wrapped to the end.");
    }

    [AvaloniaFact]
    public void Find_prefills_the_selection_and_returns_focus_to_the_canvas_on_close()
    {
        using var viewModel = new MainViewModel();
        viewModel.InsertText("Hello world");
        var paragraph = viewModel.Document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .Single();
        var run = paragraph.Runs[0];
        viewModel.Selection = new Selection(
            new TextPosition(paragraph.Id, run.Id, 0),
            new TextPosition(paragraph.Id, run.Id, 5));

        var window = new MainWindow { DataContext = viewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        viewModel.FindCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        viewModel.FindQuery.ShouldBe("Hello");

        viewModel.CloseFindCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        viewModel.IsFindPanelOpen.ShouldBeFalse();
        var canvas = window.FindControl<DocumentPreviewControl>("PageCanvas");
        canvas.ShouldNotBeNull();
        canvas.IsFocused.ShouldBeTrue();

        window.Close();
    }

    [Fact]
    public void Find_with_no_matches_does_not_move_the_selection()
    {
        using var viewModel = new MainViewModel();
        viewModel.InsertText("Hello world");
        var selectionBefore = viewModel.Selection;

        viewModel.FindCommand.Execute(null);
        viewModel.FindQuery = "missing";

        viewModel.FindMatchCount.ShouldBe(0);
        viewModel.FindStatusText.ShouldBe("No matches for \"missing\".");
        viewModel.Selection.ShouldBe(selectionBefore);
    }

    [Fact]
    public void Replace_replaces_the_active_match_and_clears_the_highlight()
    {
        using var viewModel = new MainViewModel();
        viewModel.InsertText("Hello world");
        viewModel.OpenReplaceCommand.Execute(null);
        viewModel.FindQuery = "world";
        viewModel.FindNextCommand.Execute(null);
        viewModel.ReplaceQuery = "there";

        viewModel.ReplaceCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBe("Hello there");
        viewModel.FindMatchCount.ShouldBe(0);
        viewModel.StatusText.ShouldBe("Replaced 1 match.");
    }

    [Fact]
    public void Replace_with_identical_text_advances_to_the_next_match()
    {
        using var viewModel = new MainViewModel();
        viewModel.InsertText("one one");
        viewModel.Selection = Selection.StartOf(viewModel.Document);
        viewModel.OpenReplaceCommand.Execute(null);
        viewModel.FindQuery = "one";
        viewModel.FindNextCommand.Execute(null);
        viewModel.ReplaceQuery = "one";

        viewModel.ReplaceCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBe("one one");
        viewModel.FindStatusText.ShouldBe("Match 2 of 2.");
        viewModel.StatusText.ShouldBe("Replaced 1 match.");
    }

    [Fact]
    public async Task Replace_all_requires_confirmation_and_is_one_undo_step()
    {
        using var viewModel = new MainViewModel();
        var confirmation = new FakeReplaceConfirmation(confirm: true);
        viewModel.ReplaceConfirmation = confirmation;
        viewModel.InsertText("one one one");
        viewModel.OpenReplaceCommand.Execute(null);
        viewModel.FindQuery = "one";
        viewModel.ReplaceQuery = "two";

        await viewModel.ReplaceAllCommand.ExecuteAsync(null);

        confirmation.LastCount.ShouldBe(3);
        viewModel.Document.PlainText.ShouldBe("two two two");
        viewModel.StatusText.ShouldBe("Replaced 3 matches.");

        viewModel.UndoCommand.Execute(null);

        viewModel.Document.PlainText.ShouldBe("one one one");
    }

    [Fact]
    public async Task Replace_all_does_nothing_when_confirmation_is_declined()
    {
        using var viewModel = new MainViewModel();
        viewModel.ReplaceConfirmation = new FakeReplaceConfirmation(confirm: false);
        viewModel.InsertText("one one");
        viewModel.OpenReplaceCommand.Execute(null);
        viewModel.FindQuery = "one";
        viewModel.ReplaceQuery = "two";

        await viewModel.ReplaceAllCommand.ExecuteAsync(null);

        viewModel.Document.PlainText.ShouldBe("one one");
    }

    private sealed class FakeReplaceConfirmation(bool confirm) : IReplaceConfirmation
    {
        public int LastCount { get; private set; }

        public Task<bool> ConfirmReplaceAllAsync(
            int count,
            CancellationToken cancellationToken = default)
        {
            LastCount = count;
            return Task.FromResult(confirm);
        }
    }
}
