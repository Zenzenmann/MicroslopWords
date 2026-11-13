using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Editing;
using Shouldly;

namespace MicroslopWords.Core.Tests;

public sealed class TableCommandTests
{
    [Fact]
    public void TableBlock_Create_supports_twenty_by_twenty()
    {
        var table = TableBlock.Create(20, 20);

        table.Rows.Length.ShouldBe(20);
        table.ColumnCount.ShouldBe(20);
        table.Rows[0].IsHeader.ShouldBeTrue();
        table.Rows[0].Cells.Length.ShouldBe(20);
    }

    [Fact]
    public void InsertTableCommand_inserts_a_default_table_after_the_current_paragraph()
    {
        var document = Document.CreateEmpty();
        var paragraph = document.EnumerateParagraphs().Single();
        var selection = Selection.Caret(TextNavigation.StartOf(paragraph));

        var result = new InsertTableCommand(selection).Apply(document);
        var blocks = result.Sections.Single().Blocks;

        blocks.Length.ShouldBe(3);
        blocks[0].ShouldBeOfType<ParagraphBlock>();
        var table = blocks[1].ShouldBeOfType<TableBlock>();
        table.Rows.Length.ShouldBe(3);
        table.ColumnCount.ShouldBe(3);
        table.Rows[0].IsHeader.ShouldBeTrue();
        blocks[2].ShouldBeOfType<ParagraphBlock>();
        result.FindParagraph(result.EnumerateParagraphs().First().Id).ShouldNotBeNull();
    }

    [Fact]
    public void MoveTableCellCommand_moves_forward_and_appends_a_row_at_the_end()
    {
        var (document, table) = CreateTableDocument(2, 2);
        var paragraph = table.Rows[0].Cells[0].Paragraphs[0];
        var selection = Selection.Caret(TextNavigation.StartOf(paragraph));

        for (var index = 0; index < 3; index++)
        {
            var command = new MoveTableCellCommand(selection, forward: true);
            document = command.Apply(document);
            selection = command.SelectionAfter;
        }

        var append = new MoveTableCellCommand(selection, forward: true);
        document = append.Apply(document);
        var appended = document.Sections.Single().Blocks.OfType<TableBlock>().Single();

        appended.Rows.Length.ShouldBe(3);
        appended.Rows[2].Cells.ShouldNotBeEmpty();
    }

    [Fact]
    public void TableRowCommand_inserts_and_deletes_rows()
    {
        var (document, table) = CreateTableDocument(2, 2);
        var paragraph = table.Rows[1].Cells[0].Paragraphs[0];
        var selection = Selection.Caret(TextNavigation.StartOf(paragraph));

        var withRow = new TableRowCommand(selection, insert: true, above: true)
            .Apply(document);
        withRow.Sections.Single().Blocks.OfType<TableBlock>().Single()
            .Rows.Length.ShouldBe(3);

        var withoutRow = new TableRowCommand(selection, insert: false, above: false)
            .Apply(withRow);
        withoutRow.Sections.Single().Blocks.OfType<TableBlock>().Single()
            .Rows.Length.ShouldBe(2);
    }

    [Fact]
    public void TableColumnCommand_inserts_and_deletes_columns()
    {
        var (document, table) = CreateTableDocument(2, 2);
        var paragraph = table.Rows[0].Cells[1].Paragraphs[0];
        var selection = Selection.Caret(TextNavigation.StartOf(paragraph));

        var withColumn = new TableColumnCommand(selection, insert: true, left: true)
            .Apply(document);
        withColumn.Sections.Single().Blocks.OfType<TableBlock>().Single()
            .ColumnCount.ShouldBe(3);

        var withoutColumn = new TableColumnCommand(selection, insert: false, left: false)
            .Apply(withColumn);
        withoutColumn.Sections.Single().Blocks.OfType<TableBlock>().Single()
            .ColumnCount.ShouldBe(2);
    }

    [Fact]
    public void MergeTableCellsCommand_merges_horizontal_cells_and_split_restores()
    {
        var (document, table) = CreateTableDocument(2, 2, "A", "B", "C", "D");
        var first = table.Rows[0].Cells[0].Paragraphs[0];
        var second = table.Rows[0].Cells[1].Paragraphs[0];
        var selection = new Selection(
            TextNavigation.StartOf(first),
            TextNavigation.EndOf(second));

        var merged = new MergeTableCellsCommand(selection).Apply(document);
        var mergedTable = merged.Sections.Single().Blocks.OfType<TableBlock>().Single();
        var mergedCell = mergedTable.Rows[0].Cells[0];

        mergedCell.ColumnSpan.ShouldBe(2);
        mergedCell.Paragraphs.Length.ShouldBe(2);
        mergedCell.PlainText.ShouldContain("A");
        mergedCell.PlainText.ShouldContain("B");

        var mergedSelection = Selection.Caret(
            TextNavigation.StartOf(mergedCell.Paragraphs[0]));
        var split = new SplitTableCellCommand(mergedSelection).Apply(merged);
        var splitTable = split.Sections.Single().Blocks.OfType<TableBlock>().Single();

        splitTable.Rows[0].Cells[0].ColumnSpan.ShouldBe(1);
        splitTable.Rows[0].Cells.Length.ShouldBe(2);
    }

    [Fact]
    public void MergeTableCellsCommand_merges_vertical_cells_with_continuation()
    {
        var (document, table) = CreateTableDocument(2, 2, "A", "B", "C", "D");
        var first = table.Rows[0].Cells[0].Paragraphs[0];
        var below = table.Rows[1].Cells[0].Paragraphs[0];
        var selection = new Selection(
            TextNavigation.StartOf(first),
            TextNavigation.EndOf(below));

        var merged = new MergeTableCellsCommand(selection).Apply(document);
        var mergedTable = merged.Sections.Single().Blocks.OfType<TableBlock>().Single();

        mergedTable.Rows[0].Cells[0].RowSpan.ShouldBe(2);
        mergedTable.Rows[1].Cells[0].IsMergedContinuation.ShouldBeTrue();
    }

    [Fact]
    public void SelectTableCommand_selects_all_cell_paragraphs()
    {
        var (document, table) = CreateTableDocument(2, 2, "A", "B", "C", "D");
        var selection = Selection.Caret(
            TextNavigation.StartOf(table.Rows[0].Cells[0].Paragraphs[0]));
        var command = new SelectTableCommand(selection);

        _ = command.Apply(document);

        command.SelectionAfter.IsCollapsed.ShouldBeFalse();
        command.SelectionAfter.GetStart(document).ParagraphId.ShouldBe(
            table.Rows[0].Cells[0].Paragraphs[0].Id);
        command.SelectionAfter.GetEnd(document).ParagraphId.ShouldBe(
            table.Rows[1].Cells[1].Paragraphs[0].Id);
    }

    [Fact]
    public void DeleteTableCommand_removes_the_table_and_keeps_a_paragraph()
    {
        var (document, table) = CreateTableDocument(2, 2);
        var paragraph = table.Rows[0].Cells[0].Paragraphs[0];
        var selection = Selection.Caret(TextNavigation.StartOf(paragraph));

        var deleted = new DeleteTableCommand(selection).Apply(document);

        deleted.Sections.Single().Blocks.OfType<TableBlock>().ShouldBeEmpty();
        deleted.EnumerateParagraphs().ShouldNotBeEmpty();
    }

    [Fact]
    public void TableCellFormatting_commands_set_shading_and_width()
    {
        var (document, table) = CreateTableDocument(2, 2);
        var paragraph = table.Rows[0].Cells[0].Paragraphs[0];
        var selection = Selection.Caret(TextNavigation.StartOf(paragraph));

        var shaded = new SetTableCellShadingCommand(selection, "#ABCDEF").Apply(document);
        var shadedTable = shaded.Sections.Single().Blocks.OfType<TableBlock>().Single();
        shadedTable.Rows[0].Cells[0].ShadingHex.ShouldBe("#ABCDEF");

        var sized = new SetTableColumnWidthCommand(selection, 120).Apply(shaded);
        var sizedTable = sized.Sections.Single().Blocks.OfType<TableBlock>().Single();
        sizedTable.ColumnWidths[0].ShouldBe(120);
    }

    [Fact]
    public void Document_operations_find_and_replace_paragraphs_inside_table_cells()
    {
        var (document, table) = CreateTableDocument(1, 1);
        var paragraph = table.Rows[0].Cells[0].Paragraphs[0];
        var replacement = paragraph with
        {
            Runs = ImmutableArray.Create(Run.Create("updated")),
        };

        var updated = document.ReplaceBlock(replacement);
        updated.FindParagraph(paragraph.Id)!.PlainText.ShouldBe("updated");

        var next = ParagraphBlock.Create("next");
        var inserted = updated.InsertBlockAfter(paragraph.Id, next);
        inserted.FindParagraph(next.Id).ShouldNotBeNull();

        var removed = inserted.RemoveBlock(next.Id);
        removed.FindParagraph(next.Id).ShouldBeNull();
    }

    [Fact]
    public void Delete_range_across_paragraphs_inside_one_cell_merges_within_the_cell()
    {
        var table = TableBlock.Create(1, 1, hasHeaderRow: false);
        var first = ParagraphBlock.Create("Hello");
        var second = ParagraphBlock.Create("World");
        table = table.ReplaceCell(
            table.Rows[0].Cells[0].Id,
            table.Rows[0].Cells[0] with
            {
                Paragraphs = ImmutableArray.Create(first, second),
            });
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(table));
        var document = new Document(Guid.NewGuid(), "Cell", ImmutableArray.Create(section));
        var start = new TextPosition(
            first.Id,
            first.Runs.Single().Id,
            first.PlainText.Length);
        var end = new TextPosition(
            second.Id,
            second.Runs.Single().Id,
            0);

        var updated = new DeleteSelectionCommand(new Selection(start, end)).Apply(document)
            .Sections.Single()
            .Blocks.OfType<TableBlock>()
            .Single()
            .Rows[0]
            .Cells[0];

        updated.Paragraphs.Length.ShouldBe(1);
        updated.Paragraphs[0].PlainText.ShouldBe("HelloWorld");
    }

    [Fact]
    public void Backspace_at_cell_start_does_not_merge_across_cells()
    {
        var (document, table) = CreateTableDocument(2, 2, "A", "B", "C", "D");
        var secondCell = table.Rows[0].Cells[1].Paragraphs[0];
        var selection = Selection.Caret(TextNavigation.StartOf(secondCell));

        var result = new DeleteBackwardCommand(selection).Apply(document);
        var resultTable = result.Sections.Single().Blocks.OfType<TableBlock>().Single();

        resultTable.Rows[0].Cells[0].PlainText.ShouldBe("A");
        resultTable.Rows[0].Cells[1].PlainText.ShouldBe("B");
    }

    [Fact]
    public void DocumentFragment_Capture_copies_table_structure_with_new_ids()
    {
        var (document, table) = CreateTableDocument(2, 2, "A", "B", "C", "D");
        var first = table.Rows[0].Cells[0].Paragraphs[0];
        var second = table.Rows[0].Cells[1].Paragraphs[0];
        var selection = new Selection(
            TextNavigation.StartOf(first),
            TextNavigation.EndOf(second));

        var fragment = DocumentFragment.Capture(document, selection);

        var captured = fragment.Tables.ShouldHaveSingleItem();
        captured.Id.ShouldNotBe(table.Id);
        captured.Rows[0].Cells[0].Id.ShouldNotBe(table.Rows[0].Cells[0].Id);
        captured.PlainText.ShouldContain("A");
        captured.PlainText.ShouldContain("B");
    }

    [Fact]
    public void PasteFragmentCommand_pastes_table_structure_and_undoes()
    {
        var (source, table) = CreateTableDocument(2, 2, "A", "B", "C", "D");
        var first = table.Rows[0].Cells[0].Paragraphs[0];
        var second = table.Rows[0].Cells[1].Paragraphs[0];
        var fragment = DocumentFragment.Capture(
            source,
            new Selection(TextNavigation.StartOf(first), TextNavigation.EndOf(second)));
        var target = Document.CreateEmpty();
        var paragraph = target.EnumerateParagraphs().Single();
        var command = new PasteFragmentCommand(
            Selection.Caret(TextNavigation.StartOf(paragraph)),
            fragment);

        var pasted = command.Apply(target);

        var pastedTable = pasted.Sections.Single().Blocks.OfType<TableBlock>().ShouldHaveSingleItem();
        pastedTable.Rows.Length.ShouldBe(2);
        pastedTable.PlainText.ShouldContain("A");
        command.Revert(pasted).ShouldBeSameAs(target);
    }

    private static (Document Document, TableBlock Table) CreateTableDocument(
        int rows,
        int columns,
        params string[] texts)
    {
        var table = TableBlock.Create(rows, columns, hasHeaderRow: false);
        var textIndex = 0;
        var updatedRows = table.Rows
            .Select(row => row with
            {
                Cells = row.Cells
                    .Select(cell =>
                    {
                        var text = textIndex < texts.Length
                            ? texts[textIndex++]
                            : string.Empty;

                        return cell with
                        {
                            Paragraphs = ImmutableArray.Create(ParagraphBlock.Create(text)),
                        };
                    })
                    .ToImmutableArray(),
            })
            .ToImmutableArray();
        table = table.WithRows(updatedRows);

        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(table));
        var document = new Document(Guid.NewGuid(), "Tables", ImmutableArray.Create(section));

        return (document, table);
    }
}
