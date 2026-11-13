using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using MicroslopWords.App.ViewModels;
using MicroslopWords.App.Views;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Ui.Tests;

public sealed class TableUiTests
{
    [Fact]
    public void MainViewModel_inserts_a_table_at_the_caret()
    {
        var viewModel = new MainViewModel();
        viewModel.TableRowsInput = "2";
        viewModel.TableColumnsInput = "3";

        viewModel.InsertTableCommand.Execute(null);

        var table = viewModel.Document.Sections.Single().Blocks
            .OfType<TableBlock>()
            .ShouldHaveSingleItem();
        table.Rows.Length.ShouldBe(2);
        table.ColumnCount.ShouldBe(3);
        table.HasHeaderRow.ShouldBeTrue();
        viewModel.Document.FindParagraph(viewModel.Selection.Focus.ParagraphId)
            .ShouldNotBeNull();
        viewModel.CanUndo.ShouldBeTrue();
    }

    [Fact]
    public void MainViewModel_tab_navigation_moves_cells_and_appends_a_row()
    {
        var viewModel = CreateTableViewModel(2, 2);
        var initial = viewModel.Selection.Focus.ParagraphId;

        viewModel.TryMoveTableCell(forward: true).ShouldBeTrue();
        viewModel.Selection.Focus.ParagraphId.ShouldNotBe(initial);

        viewModel.TryMoveTableCell(forward: true).ShouldBeTrue();
        viewModel.TryMoveTableCell(forward: true).ShouldBeTrue();
        viewModel.TryMoveTableCell(forward: true).ShouldBeTrue();

        var table = viewModel.Document.Sections.Single().Blocks
            .OfType<TableBlock>()
            .ShouldHaveSingleItem();
        table.Rows.Length.ShouldBe(3);
    }

    [Fact]
    public void MainViewModel_merges_and_splits_cells()
    {
        var viewModel = CreateTableViewModel(2, 2);
        var table = viewModel.Document.Sections.Single().Blocks
            .OfType<TableBlock>()
            .ShouldHaveSingleItem();
        var first = table.Rows[0].Cells[0].Paragraphs[0];
        var second = table.Rows[0].Cells[1].Paragraphs[0];

        viewModel.Selection = new Selection(
            TextNavigation.StartOf(first),
            TextNavigation.EndOf(second));
        viewModel.MergeTableCellsCommand.Execute(null);

        table = viewModel.Document.Sections.Single().Blocks
            .OfType<TableBlock>()
            .ShouldHaveSingleItem();
        table.Rows[0].Cells[0].ColumnSpan.ShouldBe(2);

        viewModel.Selection = Selection.Caret(
            TextNavigation.StartOf(table.Rows[0].Cells[0].Paragraphs[0]));
        viewModel.SplitTableCellCommand.Execute(null);

        table = viewModel.Document.Sections.Single().Blocks
            .OfType<TableBlock>()
            .ShouldHaveSingleItem();
        table.Rows[0].Cells[0].ColumnSpan.ShouldBe(1);
        table.Rows[0].Cells.Length.ShouldBe(2);
    }

    [Fact]
    public void MainViewModel_rejects_invalid_table_dimensions()
    {
        var viewModel = new MainViewModel();
        viewModel.TableRowsInput = "99";

        viewModel.InsertTableCommand.Execute(null);

        viewModel.ErrorMessage.ShouldContain("between 1 and 20");
        viewModel.Document.Sections.Single().Blocks
            .OfType<TableBlock>()
            .ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void MainWindow_binds_table_controls()
    {
        var viewModel = new MainViewModel();
        var window = new MainWindow { DataContext = viewModel };

        window.Show();
        Dispatcher.UIThread.RunJobs();

        var rows = window.FindControl<TextBox>("TableRowsBox");
        var columns = window.FindControl<TextBox>("TableColumnsBox");
        var insert = window.FindControl<Button>("InsertTableButton");
        var merge = window.FindControl<Button>("MergeCellsButton");

        rows.ShouldNotBeNull();
        columns.ShouldNotBeNull();
        insert.ShouldNotBeNull();
        merge.ShouldNotBeNull();
        AutomationProperties.GetName(insert).ShouldBe("Insert table");

        window.Close();
    }

    private static MainViewModel CreateTableViewModel(int rows, int columns)
    {
        var viewModel = new MainViewModel();
        viewModel.TableRowsInput = rows.ToString(CultureInfo.InvariantCulture);
        viewModel.TableColumnsInput = columns.ToString(CultureInfo.InvariantCulture);
        viewModel.InsertTableCommand.Execute(null);

        return viewModel;
    }
}
