using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MicroslopWords.App.Controls;
using MicroslopWords.App.ViewModels;
using MicroslopWords.App.Views;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class HeaderFooterUiTests
{
    [Fact]
    public void MainViewModel_enters_header_mode_and_types_without_changing_body()
    {
        var viewModel = new MainViewModel();
        var bodyParagraph = viewModel.Document.EnumerateParagraphs().Single();

        viewModel.EnterHeaderModeCommand.Execute(null);

        viewModel.IsHeaderFooterMode.ShouldBeTrue();
        viewModel.HeaderFooterModeLabel.ShouldContain("Header");

        viewModel.InsertText("Draft");

        var header = viewModel.Document.Sections.Single().Header.ShouldNotBeNull();
        header.PlainText.ShouldBe("Draft");
        viewModel.Document.PlainText.ShouldBeEmpty();

        viewModel.ExitHeaderFooterModeCommand.Execute(null);

        viewModel.IsHeaderFooterMode.ShouldBeFalse();
        viewModel.Selection.Focus.ParagraphId.ShouldBe(bodyParagraph.Id);
    }

    [Fact]
    public void MainViewModel_blocks_body_edits_while_in_header_mode()
    {
        var viewModel = new MainViewModel();
        var bodyParagraph = viewModel.Document.EnumerateParagraphs().Single();
        viewModel.EnterHeaderModeCommand.Execute(null);

        viewModel.Selection = Selection.Caret(TextNavigation.StartOf(bodyParagraph));
        viewModel.InsertText("Body edit");

        viewModel.Document.PlainText.ShouldBeEmpty();
        viewModel.Document.Sections.Single().Header!.PlainText.ShouldBeEmpty();
    }

    [Fact]
    public void MainViewModel_inserts_page_and_total_fields_in_header()
    {
        var viewModel = new MainViewModel();
        viewModel.EnterHeaderModeCommand.Execute(null);

        viewModel.InsertPageNumberFieldCommand.Execute(null);
        viewModel.InsertTotalPagesFieldCommand.Execute(null);

        var runs = viewModel.Document.Sections.Single().Header!
            .Blocks.OfType<ParagraphBlock>()
            .Single()
            .Runs;

        runs.ShouldContain(run => run.PageField == PageFieldKind.PageNumber);
        runs.ShouldContain(run => run.PageField == PageFieldKind.TotalPages);
    }

    [Fact]
    public void MainViewModel_toggles_different_first_page()
    {
        var viewModel = new MainViewModel();
        viewModel.EnterHeaderModeCommand.Execute(null);

        viewModel.ToggleDifferentFirstPageCommand.Execute(null);

        viewModel.Document.Sections.Single().DifferentFirstPage.ShouldBeTrue();
        viewModel.IsDifferentFirstPage.ShouldBeTrue();
    }

    [Fact]
    public void MainViewModel_undoes_header_edit()
    {
        var viewModel = new MainViewModel();
        viewModel.EnterHeaderModeCommand.Execute(null);
        viewModel.InsertText("Draft");

        viewModel.UndoCommand.Execute(null);

        viewModel.Document.Sections.Single().Header!.PlainText.ShouldBeEmpty();
        viewModel.IsHeaderFooterMode.ShouldBeTrue();
    }

    [AvaloniaFact]
    public void MainWindow_binds_header_footer_mode_controls()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        viewModel.EnterHeaderModeCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var strip = window.FindControl<Border>("HeaderFooterModeStrip");
        var canvas = window.FindControl<DocumentPreviewControl>("PageCanvas");

        strip.ShouldNotBeNull();
        strip.IsVisible.ShouldBeTrue();
        canvas.ShouldNotBeNull();
        canvas.ActiveHeaderFooterKind.ShouldBe(HeaderFooterKind.Header);

        window.Close();
    }
}
