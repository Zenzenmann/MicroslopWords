using System.Collections.Immutable;
using System.Text;

namespace MicroslopWords.Core.Documents;

public sealed record TableBorders(
    bool Left = true,
    bool Top = true,
    bool Right = true,
    bool Bottom = true,
    string ColorHex = "#000000",
    double WidthPoints = 0.5)
{
    public static TableBorders Default { get; } = new();

    public TableBorders WithoutSides() => this with
    {
        Left = false,
        Top = false,
        Right = false,
        Bottom = false,
    };
}

public sealed record TableCell(
    Guid Id,
    ImmutableArray<ParagraphBlock> Paragraphs,
    int ColumnSpan = 1,
    int RowSpan = 1,
    string? ShadingHex = null,
    TableBorders? Borders = null,
    bool IsMergedContinuation = false)
{
    public static TableCell CreateEmpty() =>
        new(Guid.NewGuid(), ImmutableArray.Create(ParagraphBlock.Create()));

    public string PlainText => string.Join('\n', Paragraphs.Select(paragraph => paragraph.PlainText));
}

public sealed record TableRow(
    Guid Id,
    ImmutableArray<TableCell> Cells,
    bool IsHeader = false)
{
    public static TableRow CreateEmpty(int columns, bool isHeader = false)
    {
        var cells = ImmutableArray.CreateBuilder<TableCell>(columns);

        for (var index = 0; index < columns; index++)
        {
            cells.Add(TableCell.CreateEmpty());
        }

        return new TableRow(Guid.NewGuid(), cells.ToImmutable(), isHeader);
    }
}

public sealed record TableBlock(
    Guid Id,
    ImmutableArray<TableRow> Rows,
    ImmutableArray<double> ColumnWidths,
    bool HasHeaderRow,
    double? WidthPoints = null,
    TableBorders? Borders = null) : Block(Id)
{
    public static TableBlock Create(
        int rows = 3,
        int columns = 3,
        bool hasHeaderRow = true,
        double contentWidthPoints = 468)
    {
        var safeRows = Math.Clamp(rows, 1, 20);
        var safeColumns = Math.Clamp(columns, 1, 20);
        var widths = ImmutableArray.CreateBuilder<double>(safeColumns);

        for (var index = 0; index < safeColumns; index++)
        {
            widths.Add(contentWidthPoints / safeColumns);
        }

        var tableRows = ImmutableArray.CreateBuilder<TableRow>(safeRows);

        for (var index = 0; index < safeRows; index++)
        {
            tableRows.Add(TableRow.CreateEmpty(
                safeColumns,
                hasHeaderRow && index == 0));
        }

        return new TableBlock(
            Guid.NewGuid(),
            tableRows.ToImmutable(),
            widths.ToImmutable(),
            hasHeaderRow,
            contentWidthPoints,
            TableBorders.Default);
    }

    public int RowCount => Rows.Length;

    public int ColumnCount => ColumnWidths.Length;

    public string PlainText
    {
        get
        {
            var builder = new StringBuilder();

            for (var rowIndex = 0; rowIndex < Rows.Length; rowIndex++)
            {
                if (rowIndex > 0)
                {
                    builder.Append('\n');
                }

                var row = Rows[rowIndex];
                builder.Append(string.Join('\t', row.Cells
                    .Where(cell => !cell.IsMergedContinuation)
                    .Select(cell => cell.PlainText)));
            }

            return builder.ToString();
        }
    }

    public TableCell? FindCellAt(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows.Length)
        {
            return null;
        }

        var gridColumn = 0;

        foreach (var cell in Rows[rowIndex].Cells)
        {
            if (cell.IsMergedContinuation)
            {
                gridColumn += Math.Max(1, cell.ColumnSpan);
                continue;
            }

            if (gridColumn <= columnIndex
                && columnIndex < gridColumn + Math.Max(1, cell.ColumnSpan))
            {
                return cell;
            }

            gridColumn += Math.Max(1, cell.ColumnSpan);
        }

        return null;
    }

    public (int Row, int Column, TableCell Cell)? FindCell(Guid cellId)
    {
        for (var rowIndex = 0; rowIndex < Rows.Length; rowIndex++)
        {
            var column = 0;

            foreach (var cell in Rows[rowIndex].Cells)
            {
                if (cell.Id == cellId)
                {
                    return (rowIndex, column, cell);
                }

                column += Math.Max(1, cell.ColumnSpan);
            }
        }

        return null;
    }

    public (int Row, int Column, TableCell Cell)? FindCellForParagraph(Guid paragraphId)
    {
        for (var rowIndex = 0; rowIndex < Rows.Length; rowIndex++)
        {
            var column = 0;

            foreach (var cell in Rows[rowIndex].Cells)
            {
                if (cell.Paragraphs.Any(paragraph => paragraph.Id == paragraphId))
                {
                    return (rowIndex, column, cell);
                }

                column += Math.Max(1, cell.ColumnSpan);
            }
        }

        return null;
    }

    public TableBlock ReplaceCell(Guid cellId, TableCell replacement)
    {
        var rows = Rows
            .Select(row => row with
            {
                Cells = row.Cells
                    .Select(cell => cell.Id == cellId ? replacement : cell)
                    .ToImmutableArray(),
            })
            .ToImmutableArray();

        return this with { Rows = rows };
    }

    public TableBlock WithRows(ImmutableArray<TableRow> rows) =>
        this with { Rows = rows };

    public TableBlock WithColumnWidths(ImmutableArray<double> widths) =>
        this with { ColumnWidths = widths };

    public TableBlock CloneWithNewIds() =>
        new(
            Guid.NewGuid(),
            Rows
                .Select(row => new TableRow(
                    Guid.NewGuid(),
                    row.Cells
                        .Select(cell => new TableCell(
                            Guid.NewGuid(),
                            cell.Paragraphs
                                .Select(paragraph => new ParagraphBlock(
                                    Guid.NewGuid(),
                                    paragraph.Runs
                                        .Select(run => new Run(
                                            Guid.NewGuid(),
                                            run.Text,
                                            run.Properties))
                                        .ToImmutableArray(),
                                    paragraph.Properties))
                                .ToImmutableArray(),
                            cell.ColumnSpan,
                            cell.RowSpan,
                            cell.ShadingHex,
                            cell.Borders,
                            cell.IsMergedContinuation))
                        .ToImmutableArray(),
                    row.IsHeader))
                .ToImmutableArray(),
            ColumnWidths,
            HasHeaderRow,
            WidthPoints,
            Borders);
}

public readonly record struct TableCellPosition(
    Guid TableId,
    int RowIndex,
    int ColumnIndex,
    Guid CellId);

public readonly record struct TableSelection(
    Guid TableId,
    int StartRow,
    int StartColumn,
    int EndRow,
    int EndColumn)
{
    public int TopRow => Math.Min(StartRow, EndRow);

    public int BottomRow => Math.Max(StartRow, EndRow);

    public int LeftColumn => Math.Min(StartColumn, EndColumn);

    public int RightColumn => Math.Max(StartColumn, EndColumn);

    public static TableSelection FromCells(
        Guid tableId,
        int firstRow,
        int firstColumn,
        int secondRow,
        int secondColumn) =>
        new(tableId, firstRow, firstColumn, secondRow, secondColumn);
}
