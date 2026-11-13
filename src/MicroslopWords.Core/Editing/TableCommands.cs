using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public sealed class InsertTableCommand(
    Selection selection,
    int rows = 3,
    int columns = 3,
    bool hasHeaderRow = true) : DocumentEditCommand
{
    public override string Description => "Insert table";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var paragraph = document.FindParagraph(current.Focus.ParagraphId);
        var result = document;
        var table = TableBlock.Create(
            Math.Clamp(rows, 1, 20),
            Math.Clamp(columns, 1, 20),
            hasHeaderRow);

        if (paragraph is null)
        {
            var section = result.Sections.FirstOrDefault();

            if (section is null)
            {
                result = Document.CreateEmpty(document.Title);
            }
            else
            {
                result = result.ReplaceSection(section with
                {
                    Blocks = section.Blocks.Add(table),
                });
            }
        }
        else
        {
            var containingTable = result.FindTableForParagraph(paragraph.Id);
            var anchorId = containingTable?.Id ?? paragraph.Id;

            result = result.InsertBlockAfter(anchorId, table);

            if (result.Sections
                .SelectMany(section => section.Blocks)
                .LastOrDefault()?.Id == table.Id)
            {
                result = result.InsertBlockAfter(table.Id, ParagraphBlock.Create());
            }
        }

        var firstParagraph = table.Rows
            .SelectMany(row => row.Cells)
            .SelectMany(cell => cell.Paragraphs)
            .First();

        selectionAfter = Selection.Caret(TextNavigation.StartOf(firstParagraph));
        return result;
    }
}

public sealed class MoveTableCellCommand(
    Selection selection,
    bool forward) : DocumentEditCommand
{
    public override string Description => forward ? "Next table cell" : "Previous table cell";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!TableEditing.TryGetCell(document, current.Focus, out var table, out var cell, out var row, out var column))
        {
            selectionAfter = current;
            return document;
        }

        var cells = TableEditing.OccupiedCells(table).ToList();
        var index = cells.FindIndex(item => item.Cell.Id == cell.Id);
        var targetIndex = forward ? index + 1 : index - 1;
        var result = document;

        if (targetIndex >= cells.Count && forward)
        {
            var newRow = TableEditing.CreateEmptyRow(table);
            table = table.WithRows(table.Rows.Add(newRow));
            result = result.ReplaceBlock(table);
            cells = TableEditing.OccupiedCells(table).ToList();
            targetIndex = cells.Count - 1;
        }

        if (index < 0 || targetIndex < 0 || targetIndex >= cells.Count)
        {
            selectionAfter = current;
            return result;
        }

        var target = cells[targetIndex];
        var paragraph = forward
            ? target.Cell.Paragraphs.First()
            : target.Cell.Paragraphs.Last();
        var position = forward
            ? TextNavigation.StartOf(paragraph)
            : TextNavigation.EndOf(paragraph);

        selectionAfter = Selection.Caret(position);
        return result;
    }
}

public sealed class TableRowCommand(
    Selection selection,
    bool insert,
    bool above) : DocumentEditCommand
{
    public override string Description => insert ? "Insert table row" : "Delete table row";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!TableEditing.TryGetCell(document, current.Focus, out var table, out var cell, out var row, out _))
        {
            selectionAfter = current;
            return document;
        }

        var result = document;

        if (insert)
        {
            if (above && row == 0 && table.HasHeaderRow)
            {
                throw new InvalidOperationException(
                    "A row cannot be inserted above the header row.");
            }

            if (table.Rows.Any(item => item.Cells.Any(candidate => candidate.RowSpan > 1)))
            {
                throw new InvalidOperationException(
                    "Rows cannot be inserted while a vertical merge spans this row.");
            }

            var newRow = TableRow.CreateEmpty(
                table.ColumnCount,
                isHeader: false);
            table = table.WithRows(table.Rows.Insert(above ? row : row + 1, newRow));
        }
        else
        {
            if (table.Rows.Length <= 1)
            {
                throw new InvalidOperationException("A table must keep at least one row.");
            }

            if (table.Rows.Any(item => item.Cells.Any(candidate =>
                    !candidate.IsMergedContinuation && candidate.RowSpan > 1)))
            {
                throw new InvalidOperationException(
                    "A row cannot be deleted while a vertical merge is active.");
            }

            table = table.WithRows(table.Rows.RemoveAt(row));
            table = TableEditing.NormalizeHeaderRow(table);
        }

        result = result.ReplaceBlock(table);
        var targetParagraph = table.Rows
            .SelectMany(item => item.Cells)
            .Where(item => !item.IsMergedContinuation)
            .SelectMany(item => item.Paragraphs)
            .FirstOrDefault() ?? cell.Paragraphs.First();

        selectionAfter = Selection.Caret(TextNavigation.StartOf(targetParagraph));
        return result;
    }
}

public sealed class TableColumnCommand(
    Selection selection,
    bool insert,
    bool left) : DocumentEditCommand
{
    public override string Description => insert ? "Insert table column" : "Delete table column";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!TableEditing.TryGetCell(document, current.Focus, out var table, out var cell, out _, out var column))
        {
            selectionAfter = current;
            return document;
        }

        var result = document;

        if (insert)
        {
            var targetColumn = left ? column : column + 1;
            TableEditing.EnsureNoMergedColumn(table, targetColumn);
            var width = table.WidthPoints is { } tableWidth
                ? tableWidth / Math.Max(1, table.ColumnCount)
                : 90;
            table = table.WithColumnWidths(table.ColumnWidths.Insert(targetColumn, width));
            table = table.WithRows(table.Rows
                .Select(row => TableEditing.TryInsertCellAt(
                    row,
                    targetColumn,
                    TableCell.CreateEmpty(),
                    out var updated)
                    ? updated
                    : row)
                .ToImmutableArray());
        }
        else
        {
            if (table.ColumnCount <= 1)
            {
                throw new InvalidOperationException("A table must keep at least one column.");
            }

            TableEditing.EnsureNoMergedColumn(table, column);
            table = table.WithColumnWidths(table.ColumnWidths.RemoveAt(column));
            table = table.WithRows(table.Rows
                .Select(row => TableEditing.TryRemoveCellAt(row, column, out var updated)
                    ? updated
                    : row)
                .ToImmutableArray());
        }

        result = result.ReplaceBlock(table);
        selectionAfter = current;
        return result;
    }
}

public sealed class SelectTableCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Select table";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var table = document.FindTableForParagraph(current.Focus.ParagraphId);

        if (table is null)
        {
            selectionAfter = current;
            return document;
        }

        var paragraphs = table.Rows
            .SelectMany(row => row.Cells)
            .Where(cell => !cell.IsMergedContinuation)
            .SelectMany(cell => cell.Paragraphs)
            .ToList();

        if (paragraphs.Count == 0)
        {
            selectionAfter = current;
            return document;
        }

        selectionAfter = new Selection(
            TextNavigation.StartOf(paragraphs[0]),
            TextNavigation.EndOf(paragraphs[^1]));

        return document;
    }
}

public sealed class DeleteTableCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Delete table";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var table = document.FindTableForParagraph(current.Focus.ParagraphId);

        if (table is null)
        {
            selectionAfter = current;
            return document;
        }

        var result = document.RemoveBlock(table.Id);
        var paragraphs = result.EnumerateParagraphs().ToList();

        if (paragraphs.Count == 0)
        {
            var section = result.Sections.FirstOrDefault();

            if (section is null)
            {
                result = Document.CreateEmpty(document.Title);
            }
            else
            {
                var paragraph = ParagraphBlock.Create();
                result = result.ReplaceSection(section with
                {
                    Blocks = section.Blocks.Add(paragraph),
                });
            }

            paragraphs = result.EnumerateParagraphs().ToList();
        }

        selectionAfter = paragraphs.Count == 0
            ? current
            : Selection.Caret(TextNavigation.StartOf(paragraphs[0]));

        return result;
    }
}

public sealed class MergeTableCellsCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Merge table cells";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!TableEditing.TryGetSelection(document, current, out var table, out var range))
        {
            selectionAfter = current;
            return document;
        }

        if (range.LeftColumn == range.RightColumn && range.TopRow == range.BottomRow)
        {
            selectionAfter = current;
            return document;
        }

        TableEditing.EnsureRectangleIsWholeCells(table, range);
        var cells = new List<TableCell>();

        for (var row = range.TopRow; row <= range.BottomRow; row++)
        {
            for (var column = range.LeftColumn; column <= range.RightColumn; column++)
            {
                var cell = table.FindCellAt(row, column);

                if (cell is not null && !cell.IsMergedContinuation && cells.All(item => item.Id != cell.Id))
                {
                    cells.Add(cell);
                }
            }
        }

        if (cells.Count == 0)
        {
            selectionAfter = current;
            return document;
        }

        var first = cells[0];
        var merged = first with
        {
            Paragraphs = cells
                .SelectMany(item => item.Paragraphs)
                .ToImmutableArray(),
            ColumnSpan = range.RightColumn - range.LeftColumn + 1,
            RowSpan = range.BottomRow - range.TopRow + 1,
            IsMergedContinuation = false,
        };
        var width = 0d;

        for (var column = range.LeftColumn; column <= range.RightColumn; column++)
        {
            width += table.ColumnWidths[column];
        }

        var rows = ImmutableArray.CreateBuilder<TableRow>(table.Rows.Length);

        for (var rowIndex = 0; rowIndex < table.Rows.Length; rowIndex++)
        {
            var row = table.Rows[rowIndex];

            if (rowIndex < range.TopRow || rowIndex > range.BottomRow)
            {
                rows.Add(row);
                continue;
            }

            var before = new List<TableCell>();
            var after = new List<TableCell>();
            var gridColumn = 0;

            foreach (var cell in row.Cells)
            {
                var span = Math.Max(1, cell.ColumnSpan);

                if (gridColumn + span <= range.LeftColumn)
                {
                    before.Add(cell);
                }
                else if (gridColumn > range.RightColumn)
                {
                    after.Add(cell);
                }

                gridColumn += span;
            }

            var rebuilt = ImmutableArray.CreateBuilder<TableCell>();
            rebuilt.AddRange(before);

            if (rowIndex == range.TopRow)
            {
                rebuilt.Add(merged);
            }
            else
            {
                rebuilt.Add(new TableCell(
                    Guid.NewGuid(),
                    ImmutableArray.Create(ParagraphBlock.Create()),
                    ColumnSpan: merged.ColumnSpan,
                    IsMergedContinuation: true));
            }

            rebuilt.AddRange(after);
            rows.Add(row with { Cells = rebuilt.ToImmutable() });
        }

        table = table.WithRows(rows.ToImmutable());

        if (width > 0)
        {
            var widths = table.ColumnWidths.ToBuilder();
            var perColumn = width / merged.ColumnSpan;

            for (var column = range.LeftColumn; column <= range.RightColumn; column++)
            {
                widths[column] = perColumn;
            }

            table = table.WithColumnWidths(widths.ToImmutable());
        }

        var result = document.ReplaceBlock(table);
        var paragraph = merged.Paragraphs.FirstOrDefault() ?? ParagraphBlock.Create();

        selectionAfter = Selection.Caret(TextNavigation.StartOf(paragraph));
        return result;
    }
}

public sealed class SplitTableCellCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Split table cell";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!TableEditing.TryGetCell(document, current.Focus, out var table, out var cell, out var row, out var column))
        {
            selectionAfter = current;
            return document;
        }

        if (cell.IsMergedContinuation
            || (cell.ColumnSpan <= 1 && cell.RowSpan <= 1))
        {
            selectionAfter = current;
            return document;
        }

        var columnSpan = Math.Max(1, cell.ColumnSpan);
        var rowSpan = Math.Max(1, cell.RowSpan);
        var rows = ImmutableArray.CreateBuilder<TableRow>(table.Rows.Length);

        for (var rowIndex = 0; rowIndex < table.Rows.Length; rowIndex++)
        {
            var item = table.Rows[rowIndex];

            if (rowIndex < row || rowIndex >= row + rowSpan)
            {
                rows.Add(item);
                continue;
            }

            var before = new List<TableCell>();
            var after = new List<TableCell>();
            var gridColumn = 0;

            foreach (var candidate in item.Cells)
            {
                var span = Math.Max(1, candidate.ColumnSpan);

                if (gridColumn + span <= column)
                {
                    before.Add(candidate);
                }
                else if (gridColumn > column + columnSpan - 1)
                {
                    after.Add(candidate);
                }

                gridColumn += span;
            }

            var rebuilt = ImmutableArray.CreateBuilder<TableCell>();
            rebuilt.AddRange(before);

            for (var index = 0; index < columnSpan; index++)
            {
                var isTopLeft = rowIndex == row && index == 0;
                rebuilt.Add(new TableCell(
                    isTopLeft ? cell.Id : Guid.NewGuid(),
                    isTopLeft
                        ? cell.Paragraphs
                        : ImmutableArray.Create(ParagraphBlock.Create()),
                    ShadingHex: cell.ShadingHex,
                    Borders: cell.Borders));
            }

            rebuilt.AddRange(after);
            rows.Add(item with { Cells = rebuilt.ToImmutable() });
        }

        table = table.WithRows(rows.ToImmutable());
        var totalWidth = 0d;

        for (var index = column; index < column + columnSpan; index++)
        {
            totalWidth += table.ColumnWidths[index];
        }

        var widths = table.ColumnWidths.ToBuilder();

        for (var index = 0; index < columnSpan; index++)
        {
            widths[column + index] = totalWidth / columnSpan;
        }

        table = table.WithColumnWidths(widths.ToImmutable());
        var result = document.ReplaceBlock(table);

        selectionAfter = Selection.Caret(TextNavigation.StartOf(cell.Paragraphs.First()));
        return result;
    }
}

public sealed class SetTableHeaderRowCommand(
    Selection selection,
    bool enabled) : DocumentEditCommand
{
    public override string Description => "Toggle table header row";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var table = document.FindTableForParagraph(current.Focus.ParagraphId);

        if (table is null)
        {
            selectionAfter = current;
            return document;
        }

        table = table with { HasHeaderRow = enabled };

        if (table.Rows.Length > 0)
        {
            var rows = table.Rows.ToBuilder();
            rows[0] = rows[0] with { IsHeader = enabled };
            table = table.WithRows(rows.ToImmutable());
        }

        selectionAfter = current;
        return document.ReplaceBlock(table);
    }
}

public sealed class SetTableCellShadingCommand(
    Selection selection,
    string? shadingHex) : DocumentEditCommand
{
    public override string Description => "Set cell shading";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!TableEditing.TryGetSelection(document, current, out var table, out var range))
        {
            selectionAfter = current;
            return document;
        }

        var rows = table.Rows
            .Select((row, rowIndex) => row with
            {
                Cells = row.Cells
                    .Select(cell =>
                    {
                        var position = table.FindCell(cell.Id);

                        return position is { } cellPosition
                            && cellPosition.Row >= range.TopRow
                            && cellPosition.Row <= range.BottomRow
                            && cellPosition.Column >= range.LeftColumn
                            && cellPosition.Column <= range.RightColumn
                            ? cell with { ShadingHex = shadingHex }
                            : cell;
                    })
                    .ToImmutableArray(),
            })
            .ToImmutableArray();

        selectionAfter = current;
        return document.ReplaceBlock(table.WithRows(rows));
    }
}

public sealed class SetTableCellBordersCommand(
    Selection selection,
    TableBorders borders) : DocumentEditCommand
{
    public override string Description => "Set cell borders";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!TableEditing.TryGetSelection(document, current, out var table, out var range))
        {
            selectionAfter = current;
            return document;
        }

        var rows = table.Rows
            .Select(row => row with
            {
                Cells = row.Cells
                    .Select(cell =>
                    {
                        var position = table.FindCell(cell.Id);

                        return position is { } cellPosition
                            && cellPosition.Row >= range.TopRow
                            && cellPosition.Row <= range.BottomRow
                            && cellPosition.Column >= range.LeftColumn
                            && cellPosition.Column <= range.RightColumn
                            ? cell with { Borders = borders }
                            : cell;
                    })
                    .ToImmutableArray(),
            })
            .ToImmutableArray();

        selectionAfter = current;
        return document.ReplaceBlock(table.WithRows(rows));
    }
}

public sealed class SetTableColumnWidthCommand(
    Selection selection,
    double widthPoints) : DocumentEditCommand
{
    public override string Description => "Set table column width";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!TableEditing.TryGetCell(document, current.Focus, out var table, out _, out _, out var column))
        {
            selectionAfter = current;
            return document;
        }

        var widths = table.ColumnWidths.ToBuilder();
        widths[column] = Math.Clamp(widthPoints, 18, 1000);
        selectionAfter = current;

        return document.ReplaceBlock(table.WithColumnWidths(widths.ToImmutable()));
    }
}

public sealed class SetTableWidthCommand(
    Selection selection,
    double? widthPoints) : DocumentEditCommand
{
    public override string Description => "Set table width";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var table = document.FindTableForParagraph(current.Focus.ParagraphId);

        if (table is null)
        {
            selectionAfter = current;
            return document;
        }

        selectionAfter = current;
        return document.ReplaceBlock(table with
        {
            WidthPoints = widthPoints is null
                ? null
                : Math.Clamp(widthPoints.Value, 36, 2000),
        });
    }
}

internal static class TableEditing
{
    public static bool TryGetCell(
        Document document,
        TextPosition position,
        out TableBlock table,
        out TableCell cell,
        out int row,
        out int column)
    {
        foreach (var candidate in document.Sections
                     .SelectMany(section => section.Blocks)
                     .OfType<TableBlock>())
        {
            for (var rowIndex = 0; rowIndex < candidate.Rows.Length; rowIndex++)
            {
                var gridColumn = 0;

                foreach (var item in candidate.Rows[rowIndex].Cells)
                {
                    if (item.Paragraphs.Any(paragraph => paragraph.Id == position.ParagraphId))
                    {
                        table = candidate;
                        cell = item;
                        row = rowIndex;
                        column = gridColumn;
                        return true;
                    }

                    gridColumn += Math.Max(1, item.ColumnSpan);
                }
            }
        }

        table = null!;
        cell = null!;
        row = -1;
        column = -1;
        return false;
    }

    public static bool TryGetSelection(
        Document document,
        Selection selection,
        out TableBlock table,
        out TableSelection range)
    {
        var start = selection.GetStart(document);
        var end = selection.GetEnd(document);

        if (!TryGetCell(document, start, out var startTable, out _, out var startRow, out var startColumn)
            || !TryGetCell(document, end, out var endTable, out _, out var endRow, out var endColumn)
            || startTable.Id != endTable.Id)
        {
            table = null!;
            range = default;
            return false;
        }

        table = startTable;
        range = TableSelection.FromCells(
            startTable.Id,
            startRow,
            startColumn,
            endRow,
            endColumn);

        return true;
    }

    public static IEnumerable<(int Row, int Column, TableCell Cell)> OccupiedCells(
        TableBlock table)
    {
        for (var rowIndex = 0; rowIndex < table.Rows.Length; rowIndex++)
        {
            var column = 0;

            foreach (var cell in table.Rows[rowIndex].Cells)
            {
                if (!cell.IsMergedContinuation)
                {
                    yield return (rowIndex, column, cell);
                }

                column += Math.Max(1, cell.ColumnSpan);
            }
        }
    }

    public static TableRow CreateEmptyRow(TableBlock table)
    {
        var cells = ImmutableArray.CreateBuilder<TableCell>(table.ColumnCount);

        var column = 0;

        while (column < table.ColumnCount)
        {
            var covering = table.FindCellAt(table.Rows.Length - 1, column);
            var origin = covering is null ? null : table.FindCell(covering.Id);

            if (origin is { } position
                && position.Cell.RowSpan > 1
                && table.Rows.Length - 1 < position.Row + position.Cell.RowSpan)
            {
                var span = Math.Max(1, position.Cell.ColumnSpan);

                cells.Add(new TableCell(
                    Guid.NewGuid(),
                    ImmutableArray.Create(ParagraphBlock.Create()),
                    ColumnSpan: span,
                    IsMergedContinuation: true));
                column += span;
            }
            else
            {
                cells.Add(TableCell.CreateEmpty());
                column++;
            }
        }

        return new TableRow(Guid.NewGuid(), cells.ToImmutable());
    }

    public static TableBlock NormalizeHeaderRow(TableBlock table)
    {
        if (table.Rows.Length == 0)
        {
            return table;
        }

        var rows = table.Rows.ToBuilder();
        rows[0] = rows[0] with { IsHeader = table.HasHeaderRow };
        return table.WithRows(rows.ToImmutable());
    }

    public static bool TryInsertCellAt(
        TableRow row,
        int column,
        TableCell cell,
        out TableRow updated)
    {
        var cells = row.Cells.ToBuilder();
        var gridColumn = 0;

        for (var index = 0; index < cells.Count; index++)
        {
            var candidate = cells[index];
            var span = Math.Max(1, candidate.ColumnSpan);

            if (gridColumn == column)
            {
                cells.Insert(index, cell);
                updated = row with { Cells = cells.ToImmutable() };
                return true;
            }

            if (column > gridColumn && column < gridColumn + span)
            {
                updated = row;
                return false;
            }

            gridColumn += span;
        }

        if (gridColumn != column)
        {
            updated = row;
            return false;
        }

        cells.Add(cell);
        updated = row with { Cells = cells.ToImmutable() };
        return true;
    }

    public static bool TryRemoveCellAt(
        TableRow row,
        int column,
        out TableRow updated)
    {
        var cells = row.Cells.ToBuilder();
        var gridColumn = 0;

        for (var index = 0; index < cells.Count; index++)
        {
            var candidate = cells[index];

            if (gridColumn == column)
            {
                if (candidate.ColumnSpan > 1)
                {
                    updated = row;
                    return false;
                }

                cells.RemoveAt(index);
                updated = row with { Cells = cells.ToImmutable() };
                return true;
            }

            gridColumn += Math.Max(1, candidate.ColumnSpan);
        }

        updated = row;
        return false;
    }

    public static void EnsureNoMergedColumn(TableBlock table, int column)
    {
        foreach (var row in table.Rows)
        {
            var gridColumn = 0;

            foreach (var cell in row.Cells)
            {
                var span = Math.Max(1, cell.ColumnSpan);

                if (span > 1
                    && column >= gridColumn
                    && column < gridColumn + span)
                {
                    throw new InvalidOperationException(
                        "The column intersects a merged cell.");
                }

                gridColumn += span;
            }
        }
    }

    public static void EnsureRectangleIsWholeCells(TableBlock table, TableSelection range)
    {
        for (var row = range.TopRow; row <= range.BottomRow; row++)
        {
            for (var column = range.LeftColumn; column <= range.RightColumn; column++)
            {
                var cell = table.FindCellAt(row, column);

                if (cell is null)
                {
                    throw new InvalidOperationException("The selected cell range is invalid.");
                }

                var origin = table.FindCell(cell.Id);

                if (origin is not { } position)
                {
                    throw new InvalidOperationException("The selected cell range is invalid.");
                }

                if (position.Row < range.TopRow
                    || position.Row + Math.Max(1, position.Cell.RowSpan) - 1 > range.BottomRow
                    || position.Column < range.LeftColumn
                    || position.Column + Math.Max(1, position.Cell.ColumnSpan) - 1 > range.RightColumn)
                {
                    throw new InvalidOperationException(
                        "The selection cuts through a merged cell.");
                }
            }
        }
    }

    public static int ColumnOfCell(TableRow row, Guid cellId)
    {
        var column = 0;

        foreach (var cell in row.Cells)
        {
            if (cell.Id == cellId)
            {
                return column;
            }

            column += Math.Max(1, cell.ColumnSpan);
        }

        return -1;
    }
}
