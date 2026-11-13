using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using Shouldly;

namespace MicroslopWords.Layout.Tests;

public sealed class LayoutTableTests
{
    [Fact]
    public void Layout_emits_table_rows_and_cells_inside_the_page()
    {
        var document = CreateDocument(CreateTable(2, 2, "A", "B", "C", "D"));

        var layout = new LayoutEngine().Layout(document);
        var table = layout.Pages[0].Tables.ShouldHaveSingleItem();

        table.Rows.Length.ShouldBe(2);
        table.Rows[0].Cells.Length.ShouldBe(2);
        table.Rows[0].Bounds.Y.ShouldBeGreaterThanOrEqualTo(72);
        table.Rows[1].Bounds.Y.ShouldBeGreaterThanOrEqualTo(table.Rows[0].Bounds.Bottom);
        table.Bounds.Right.ShouldBeLessThanOrEqualTo(612 - 72 + 0.001);
    }

    [Fact]
    public void Layout_keeps_rows_intact_and_repeats_header_on_new_pages()
    {
        var table = CreateTable(20, 2, "header 1", "header 2");
        var rows = table.Rows.ToBuilder();

        for (var index = 1; index < rows.Count; index++)
        {
            rows[index] = rows[index] with
            {
                Cells = rows[index].Cells
                    .Select((cell, column) => cell with
                    {
                        Paragraphs = ImmutableArray.Create(
                            ParagraphBlock.Create(string.Join(
                                "\n",
                                Enumerable.Repeat($"row {index} cell {column}", 6)))),
                    })
                    .ToImmutableArray(),
            };
        }

        table = table.WithRows(rows.ToImmutable());
        var document = CreateDocument(table);
        var layout = new LayoutEngine().Layout(document);

        layout.Pages.Length.ShouldBeGreaterThan(1);
        var continuation = layout.Pages[1].Tables.ShouldHaveSingleItem();

        continuation.Rows[0].IsHeader.ShouldBeTrue();
        continuation.ContinuesFromPreviousPage.ShouldBeTrue();
        continuation.Rows.Length.ShouldBeGreaterThan(1);
        continuation.Rows.ShouldAllBe(row => row.Bounds.Height > 0);
    }

    [Fact]
    public void Layout_respects_table_width_and_column_widths()
    {
        var table = CreateTable(2, 3, "A", "B", "C", "D", "E", "F") with
        {
            WidthPoints = 300,
        };
        var document = CreateDocument(table);
        var layout = new LayoutEngine().Layout(document);
        var tableLayout = layout.Pages[0].Tables.ShouldHaveSingleItem();

        tableLayout.Bounds.Width.ShouldBeLessThanOrEqualTo(300.001);
        tableLayout.Rows[0].Cells.Sum(cell => cell.Bounds.Width)
            .ShouldBe(tableLayout.Bounds.Width, 0.001);
    }

    [Fact]
    public void Layout_places_cell_text_and_caret_inside_the_cell()
    {
        var table = CreateTable(1, 1, "Cell text");
        var document = CreateDocument(table);
        var layout = new LayoutEngine().Layout(document);
        var cell = layout.Pages[0].Tables.Single().Rows[0].Cells.Single();
        var paragraph = cell.Paragraphs.Single();
        var line = paragraph.Lines.ShouldHaveSingleItem();

        line.Bounds.X.ShouldBeGreaterThan(cell.Bounds.X);
        line.Bounds.Right.ShouldBeLessThanOrEqualTo(cell.Bounds.Right);
        line.Bounds.Y.ShouldBeGreaterThan(cell.Bounds.Y);
        line.Bounds.Bottom.ShouldBeLessThanOrEqualTo(cell.Bounds.Bottom);

        var caret = LayoutHitTesting.FindCaret(layout, paragraph.ParagraphId, 0);
        caret.ShouldNotBeNull();
        caret!.Bounds.X.ShouldBeGreaterThanOrEqualTo(cell.Bounds.X);
    }

    [Fact]
    public void Layout_expands_vertically_merged_cell_across_its_rows()
    {
        var table = CreateTable(2, 2, "A", "B", "C", "D") with
        {
            HasHeaderRow = false,
        };
        var rows = table.Rows.ToBuilder();
        var merged = (rows[0].Cells[0] with { RowSpan = 2, ColumnSpan = 1 });
        rows[0] = rows[0] with { Cells = rows[0].Cells.SetItem(0, merged) };
        rows[1] = rows[1] with
        {
            Cells = rows[1].Cells.SetItem(
                0,
                new TableCell(
                    Guid.NewGuid(),
                    ImmutableArray.Create(ParagraphBlock.Create()),
                    IsMergedContinuation: true)),
        };
        table = table.WithRows(rows.ToImmutable());
        var document = CreateDocument(table);

        var layout = new LayoutEngine().Layout(document);
        var tableLayout = layout.Pages[0].Tables.Single();
        var mergedCell = tableLayout.Rows[0].Cells[0];

        mergedCell.Bounds.Height.ShouldBe(
            tableLayout.Rows[0].Bounds.Height + tableLayout.Rows[1].Bounds.Height,
            0.001);
    }

    [Fact]
    public void Layout_matches_golden_table_geometry_for_fixed_input()
    {
        var table = CreateTable(2, 2, "Alpha", "Beta", "Gamma", "Delta");
        var document = CreateDocument(table);

        var layout = new LayoutEngine().Layout(document);
        var tableLayout = layout.Pages.ShouldHaveSingleItem().Tables.ShouldHaveSingleItem();

        tableLayout.Bounds.X.ShouldBe(72);
        tableLayout.Bounds.Y.ShouldBe(72);
        tableLayout.Bounds.Width.ShouldBe(468, 0.001);
        tableLayout.Rows.Length.ShouldBe(2);
        tableLayout.Rows[0].Bounds.Height.ShouldBe(20.65, 0.01);
        tableLayout.Rows[1].Bounds.Y.ShouldBe(92.65, 0.01);
        tableLayout.Rows[0].Cells[0].Bounds.Width.ShouldBe(234, 0.001);
        tableLayout.Rows[0].Cells[0]
            .Paragraphs.Single()
            .Lines.Single()
            .Bounds.X.ShouldBe(76, 0.001);
    }

    [Fact]
    public void Layout_places_following_paragraph_below_the_table()
    {
        var table = CreateTable(2, 2, "A", "B", "C", "D");
        var paragraph = ParagraphBlock.Create("After");
        var section = new Section(
            Guid.NewGuid(),
            ImmutableArray.Create<Block>(table, paragraph));
        var document = new Document(
            Guid.NewGuid(),
            "Tables",
            ImmutableArray.Create(section));
        var layout = new LayoutEngine().Layout(document);
        var tableLayout = layout.Pages[0].Tables.ShouldHaveSingleItem();
        var after = layout.Pages[0].Paragraphs.ShouldHaveSingleItem();

        after.Bounds.Y.ShouldBeGreaterThanOrEqualTo(tableLayout.Bounds.Bottom);
    }

    private static TableBlock CreateTable(
        int rows,
        int columns,
        params string[] texts)
    {
        var table = TableBlock.Create(rows, columns, hasHeaderRow: true);
        var index = 0;
        var updatedRows = table.Rows
            .Select(row => row with
            {
                Cells = row.Cells
                    .Select(cell => cell with
                    {
                        Paragraphs = ImmutableArray.Create(
                            ParagraphBlock.Create(
                                index < texts.Length ? texts[index++] : string.Empty)),
                    })
                    .ToImmutableArray(),
            })
            .ToImmutableArray();

        return table.WithRows(updatedRows);
    }

    private static Document CreateDocument(TableBlock table)
    {
        var section = new Section(Guid.NewGuid(), ImmutableArray.Create<Block>(table));

        return new Document(Guid.NewGuid(), "Tables", ImmutableArray.Create(section));
    }
}
