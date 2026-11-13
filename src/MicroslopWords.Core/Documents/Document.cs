using System.Collections.Immutable;
using MicroslopWords.Core.Styles;

namespace MicroslopWords.Core.Documents;

public sealed record Document(
    Guid Id,
    string Title,
    ImmutableArray<Section> Sections,
    StyleSheet? Styles = null)
{
    public static Document CreateEmpty(string title = "Untitled") =>
        new(
            Guid.NewGuid(),
            title,
            ImmutableArray.Create(Section.CreateEmpty()),
            StyleSheet.Default);

    public StyleSheet EffectiveStyles => Styles ?? StyleSheet.Default;

    public string PlainText =>
        string.Join(
            Environment.NewLine,
            Sections
                .SelectMany(section => section.Blocks)
                .Select(block => block switch
                {
                    ParagraphBlock paragraph => paragraph.PlainText,
                    TableBlock table => table.PlainText,
                    _ => string.Empty,
                }));

    public IEnumerable<ParagraphBlock> EnumerateParagraphs() =>
        Sections
            .SelectMany(section => section.Blocks)
            .SelectMany(EnumerateParagraphs);

    public Document ReplaceBlock(Block replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var sections = Sections
            .Select(section => section with
            {
                Blocks = ReplaceInBlocks(section.Blocks, replacement),
            })
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    public ParagraphBlock? FindParagraph(Guid paragraphId) =>
        EnumerateParagraphs().FirstOrDefault(paragraph => paragraph.Id == paragraphId);

    public TableBlock? FindTable(Guid tableId) =>
        Sections
            .SelectMany(section => section.Blocks)
            .OfType<TableBlock>()
            .FirstOrDefault(table => table.Id == tableId);

    public TableBlock? FindTableForParagraph(Guid paragraphId)
    {
        foreach (var table in Sections
                     .SelectMany(section => section.Blocks)
                     .OfType<TableBlock>())
        {
            if (table.Rows.Any(row => row.Cells.Any(cell =>
                    cell.Paragraphs.Any(paragraph => paragraph.Id == paragraphId))))
            {
                return table;
            }
        }

        return null;
    }

    public Document InsertBlockAfter(Guid afterBlockId, Block block)
    {
        ArgumentNullException.ThrowIfNull(block);

        var sections = Sections
            .Select(section =>
            {
                var (blocks, inserted) = InsertInBlocks(section.Blocks, afterBlockId, block);

                return inserted ? section with { Blocks = blocks } : section;
            })
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    public Document RemoveBlock(Guid blockId)
    {
        var sections = Sections
            .Select(section =>
            {
                var (blocks, removed) = RemoveInBlocks(section.Blocks, blockId);

                return removed ? section with { Blocks = blocks } : section;
            })
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    public Document ReplaceSection(Section replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var sections = Sections
            .Select(section => section.Id == replacement.Id ? replacement : section)
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    private static IEnumerable<ParagraphBlock> EnumerateParagraphs(Block block)
    {
        if (block is ParagraphBlock paragraph)
        {
            yield return paragraph;
            yield break;
        }

        if (block is not TableBlock table)
        {
            yield break;
        }

        foreach (var cell in table.Rows.SelectMany(row => row.Cells))
        {
            foreach (var cellParagraph in cell.Paragraphs)
            {
                yield return cellParagraph;
            }
        }
    }

    private static ImmutableArray<Block> ReplaceInBlocks(
        ImmutableArray<Block> blocks,
        Block replacement)
    {
        var builder = ImmutableArray.CreateBuilder<Block>(blocks.Length);

        foreach (var block in blocks)
        {
            if (block.Id == replacement.Id)
            {
                builder.Add(replacement);
            }
            else if (block is TableBlock table)
            {
                builder.Add(ReplaceInTable(table, replacement));
            }
            else
            {
                builder.Add(block);
            }
        }

        return builder.MoveToImmutable();
    }

    private static TableBlock ReplaceInTable(TableBlock table, Block replacement)
    {
        if (replacement is not ParagraphBlock paragraph)
        {
            return table;
        }

        var rows = table.Rows
            .Select(row => row with
            {
                Cells = row.Cells
                    .Select(cell => cell.Paragraphs.Any(item => item.Id == paragraph.Id)
                        ? cell with
                        {
                            Paragraphs = cell.Paragraphs
                                .Select(item => item.Id == paragraph.Id ? paragraph : item)
                                .ToImmutableArray(),
                        }
                        : cell)
                    .ToImmutableArray(),
            })
            .ToImmutableArray();

        return table.WithRows(rows);
    }

    private static (ImmutableArray<Block> Blocks, bool Inserted) InsertInBlocks(
        ImmutableArray<Block> blocks,
        Guid afterBlockId,
        Block block)
    {
        var index = IndexOfBlock(blocks, afterBlockId);

        if (index >= 0)
        {
            return (blocks.Insert(index + 1, block), true);
        }

        var builder = ImmutableArray.CreateBuilder<Block>(blocks.Length);
        var inserted = false;

        foreach (var candidate in blocks)
        {
            if (!inserted && candidate is TableBlock table)
            {
                var (updatedTable, tableInserted) = InsertInTable(table, afterBlockId, block);

                if (tableInserted)
                {
                    builder.Add(updatedTable);
                    inserted = true;
                    continue;
                }
            }

            builder.Add(candidate);
        }

        return (builder.MoveToImmutable(), inserted);
    }

    private static (TableBlock Table, bool Inserted) InsertInTable(
        TableBlock table,
        Guid afterBlockId,
        Block block)
    {
        if (block is not ParagraphBlock paragraph)
        {
            return (table, false);
        }

        var rows = table.Rows
            .Select(row => row with
            {
                Cells = row.Cells
                    .Select(cell => InsertInCell(cell, afterBlockId, paragraph))
                    .ToImmutableArray(),
            })
            .ToImmutableArray();
        var inserted = rows
            .SelectMany(row => row.Cells)
            .Any(cell => cell.Paragraphs.Any(item => item.Id == paragraph.Id));

        return inserted ? (table.WithRows(rows), true) : (table, false);
    }

    private static TableCell InsertInCell(
        TableCell cell,
        Guid afterBlockId,
        ParagraphBlock paragraph)
    {
        var index = -1;

        for (var itemIndex = 0; itemIndex < cell.Paragraphs.Length; itemIndex++)
        {
            if (cell.Paragraphs[itemIndex].Id == afterBlockId)
            {
                index = itemIndex;
                break;
            }
        }

        return index < 0
            ? cell
            : cell with { Paragraphs = cell.Paragraphs.Insert(index + 1, paragraph) };
    }

    private static (ImmutableArray<Block> Blocks, bool Removed) RemoveInBlocks(
        ImmutableArray<Block> blocks,
        Guid blockId)
    {
        var index = IndexOfBlock(blocks, blockId);

        if (index >= 0)
        {
            return (blocks.RemoveAt(index), true);
        }

        var builder = ImmutableArray.CreateBuilder<Block>(blocks.Length);
        var removed = false;

        foreach (var candidate in blocks)
        {
            if (!removed && candidate is TableBlock table)
            {
                var (updatedTable, tableRemoved) = RemoveInTable(table, blockId);

                if (tableRemoved)
                {
                    builder.Add(updatedTable);
                    removed = true;
                    continue;
                }
            }

            builder.Add(candidate);
        }

        return (builder.MoveToImmutable(), removed);
    }

    private static (TableBlock Table, bool Removed) RemoveInTable(
        TableBlock table,
        Guid blockId)
    {
        var rows = table.Rows
            .Select(row => row with
            {
                Cells = row.Cells
                    .Select(cell => RemoveInCell(cell, blockId))
                    .ToImmutableArray(),
            })
            .ToImmutableArray();
        var removed = rows
            .SelectMany(row => row.Cells)
            .Any(cell => cell.Paragraphs.All(item => item.Id != blockId))
            && table.Rows
                .SelectMany(row => row.Cells)
                .Any(cell => cell.Paragraphs.Any(item => item.Id == blockId));

        return removed ? (table.WithRows(rows), true) : (table, false);
    }

    private static TableCell RemoveInCell(TableCell cell, Guid paragraphId)
    {
        var paragraphs = cell.Paragraphs
            .Where(paragraph => paragraph.Id != paragraphId)
            .ToImmutableArray();

        if (paragraphs.Length == cell.Paragraphs.Length)
        {
            return cell;
        }

        return cell with
        {
            Paragraphs = paragraphs.Length == 0
                ? ImmutableArray.Create(ParagraphBlock.Create())
                : paragraphs,
        };
    }

    private static int IndexOfBlock(ImmutableArray<Block> blocks, Guid blockId)
    {
        for (var index = 0; index < blocks.Length; index++)
        {
            if (blocks[index].Id == blockId)
            {
                return index;
            }
        }

        return -1;
    }
}
