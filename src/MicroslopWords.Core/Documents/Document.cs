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

    public IEnumerable<ParagraphBlock> EnumerateHeaderFooterParagraphs() =>
        EnumerateStoredHeaderFooters()
            .SelectMany(item => item.HeaderFooter.Blocks)
            .SelectMany(EnumerateParagraphs);

    public IEnumerable<ParagraphBlock> EnumerateAllParagraphs() =>
        EnumerateParagraphs().Concat(EnumerateHeaderFooterParagraphs());

    public Document ReplaceBlock(Block replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var sections = Sections
            .Select(section => section with
            {
                Blocks = ReplaceInBlocks(section.Blocks, replacement),
                Header = ReplaceInHeaderFooter(section.Header, replacement),
                Footer = ReplaceInHeaderFooter(section.Footer, replacement),
                FirstPageHeader = ReplaceInHeaderFooter(section.FirstPageHeader, replacement),
                FirstPageFooter = ReplaceInHeaderFooter(section.FirstPageFooter, replacement),
            })
            .ToImmutableArray();

        return this with { Sections = sections };
    }

    public ParagraphBlock? FindParagraph(Guid paragraphId) =>
        EnumerateAllParagraphs().FirstOrDefault(paragraph => paragraph.Id == paragraphId);

    public HeaderFooterZone? FindHeaderFooterZone(Guid paragraphId)
    {
        foreach (var (sectionIndex, kind, variant, headerFooter) in EnumerateStoredHeaderFooters())
        {
            if (headerFooter.Blocks
                .SelectMany(EnumerateParagraphs)
                .Any(paragraph => paragraph.Id == paragraphId))
            {
                return new HeaderFooterZone(sectionIndex, kind, variant);
            }
        }

        return null;
    }

    public int FindSectionIndexForParagraph(Guid paragraphId)
    {
        if (FindHeaderFooterZone(paragraphId) is { } zone)
        {
            return zone.SectionIndex;
        }

        for (var sectionIndex = 0; sectionIndex < Sections.Length; sectionIndex++)
        {
            if (Sections[sectionIndex].Blocks
                .SelectMany(EnumerateParagraphs)
                .Any(paragraph => paragraph.Id == paragraphId))
            {
                return sectionIndex;
            }
        }

        return 0;
    }

    public HeaderFooter? ResolveHeaderFooter(
        int sectionIndex,
        HeaderFooterKind kind,
        HeaderFooterVariant variant)
    {
        if (Sections.Length == 0)
        {
            return null;
        }

        sectionIndex = Math.Clamp(sectionIndex, 0, Sections.Length - 1);

        for (var index = sectionIndex; index >= 0; index--)
        {
            var section = Sections[index];

            if (section.LinksHeaderFooter(kind))
            {
                continue;
            }

            return section.GetHeaderFooter(kind, variant);
        }

        return null;
    }

    public int FindHeaderFooterOwnerIndex(int sectionIndex, HeaderFooterKind kind)
    {
        if (Sections.Length == 0)
        {
            return 0;
        }

        sectionIndex = Math.Clamp(sectionIndex, 0, Sections.Length - 1);

        for (var index = sectionIndex; index >= 0; index--)
        {
            if (!Sections[index].LinksHeaderFooter(kind))
            {
                return index;
            }
        }

        return 0;
    }

    public (Document Document, int OwnerIndex, HeaderFooter HeaderFooter)
        EnsureHeaderFooter(
            int sectionIndex,
            HeaderFooterKind kind,
            HeaderFooterVariant variant)
    {
        var ownerIndex = FindHeaderFooterOwnerIndex(sectionIndex, kind);
        var owner = Sections[ownerIndex];
        var existing = owner.GetHeaderFooter(kind, variant);

        if (existing is not null && !existing.IsEmpty)
        {
            return (this, ownerIndex, existing);
        }

        var created = HeaderFooter.CreateEmpty();
        var updatedOwner = owner.WithHeaderFooter(kind, variant, created);

        return (
            ReplaceSection(updatedOwner),
            ownerIndex,
            created);
    }

    public Document SetHeaderFooter(
        int sectionIndex,
        HeaderFooterKind kind,
        HeaderFooterVariant variant,
        HeaderFooter value)
    {
        if (sectionIndex < 0 || sectionIndex >= Sections.Length)
        {
            return this;
        }

        var sections = Sections.ToBuilder();
        sections[sectionIndex] = sections[sectionIndex]
            .WithHeaderFooter(kind, variant, value);

        return this with { Sections = sections.ToImmutable() };
    }

    public Document SetHeaderFooterLink(
        int sectionIndex,
        HeaderFooterKind kind,
        bool linked,
        HeaderFooter? copy)
    {
        if (sectionIndex < 0 || sectionIndex >= Sections.Length)
        {
            return this;
        }

        var section = Sections[sectionIndex].WithLinkToPrevious(kind, linked);

        if (linked)
        {
            section = section
                .WithHeaderFooter(kind, HeaderFooterVariant.Default, null)
                .WithHeaderFooter(kind, HeaderFooterVariant.FirstPage, null);
        }
        else if (copy is not null)
        {
            section = section
                .WithHeaderFooter(kind, HeaderFooterVariant.Default, copy)
                .WithHeaderFooter(kind, HeaderFooterVariant.FirstPage, null);
        }

        return ReplaceSection(section);
    }

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

                if (inserted)
                {
                    return section with { Blocks = blocks };
                }

                var (header, headerInserted) = InsertInHeaderFooter(
                    section.Header,
                    afterBlockId,
                    block);

                if (headerInserted)
                {
                    return section with { Header = header };
                }

                var (footer, footerInserted) = InsertInHeaderFooter(
                    section.Footer,
                    afterBlockId,
                    block);

                if (footerInserted)
                {
                    return section with { Footer = footer };
                }

                var (firstHeader, firstHeaderInserted) = InsertInHeaderFooter(
                    section.FirstPageHeader,
                    afterBlockId,
                    block);

                if (firstHeaderInserted)
                {
                    return section with { FirstPageHeader = firstHeader };
                }

                var (firstFooter, firstFooterInserted) = InsertInHeaderFooter(
                    section.FirstPageFooter,
                    afterBlockId,
                    block);

                return firstFooterInserted
                    ? section with { FirstPageFooter = firstFooter }
                    : section;
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

                if (removed)
                {
                    return section with { Blocks = blocks };
                }

                var (header, headerRemoved) = RemoveInHeaderFooter(section.Header, blockId);

                if (headerRemoved)
                {
                    return section with { Header = header };
                }

                var (footer, footerRemoved) = RemoveInHeaderFooter(section.Footer, blockId);

                if (footerRemoved)
                {
                    return section with { Footer = footer };
                }

                var (firstHeader, firstHeaderRemoved) = RemoveInHeaderFooter(
                    section.FirstPageHeader,
                    blockId);

                if (firstHeaderRemoved)
                {
                    return section with { FirstPageHeader = firstHeader };
                }

                var (firstFooter, firstFooterRemoved) = RemoveInHeaderFooter(
                    section.FirstPageFooter,
                    blockId);

                return firstFooterRemoved
                    ? section with { FirstPageFooter = firstFooter }
                    : section;
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

    private IEnumerable<(int SectionIndex, HeaderFooterKind Kind, HeaderFooterVariant Variant, HeaderFooter HeaderFooter)>
        EnumerateStoredHeaderFooters()
    {
        for (var sectionIndex = 0; sectionIndex < Sections.Length; sectionIndex++)
        {
            var section = Sections[sectionIndex];

            if (section.Header is not null)
            {
                yield return (
                    sectionIndex,
                    HeaderFooterKind.Header,
                    HeaderFooterVariant.Default,
                    section.Header);
            }

            if (section.FirstPageHeader is not null)
            {
                yield return (
                    sectionIndex,
                    HeaderFooterKind.Header,
                    HeaderFooterVariant.FirstPage,
                    section.FirstPageHeader);
            }

            if (section.Footer is not null)
            {
                yield return (
                    sectionIndex,
                    HeaderFooterKind.Footer,
                    HeaderFooterVariant.Default,
                    section.Footer);
            }

            if (section.FirstPageFooter is not null)
            {
                yield return (
                    sectionIndex,
                    HeaderFooterKind.Footer,
                    HeaderFooterVariant.FirstPage,
                    section.FirstPageFooter);
            }
        }
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

    private static HeaderFooter? ReplaceInHeaderFooter(
        HeaderFooter? headerFooter,
        Block replacement) =>
        headerFooter is null
            ? null
            : headerFooter with
            {
                Blocks = ReplaceInBlocks(headerFooter.Blocks, replacement),
            };

    private static (HeaderFooter? HeaderFooter, bool Inserted) InsertInHeaderFooter(
        HeaderFooter? headerFooter,
        Guid afterBlockId,
        Block block)
    {
        if (headerFooter is null)
        {
            return (null, false);
        }

        var (blocks, inserted) = InsertInBlocks(headerFooter.Blocks, afterBlockId, block);

        return inserted
            ? (headerFooter with { Blocks = blocks }, true)
            : (headerFooter, false);
    }

    private static (HeaderFooter? HeaderFooter, bool Removed) RemoveInHeaderFooter(
        HeaderFooter? headerFooter,
        Guid blockId)
    {
        if (headerFooter is null)
        {
            return (null, false);
        }

        var (blocks, removed) = RemoveInBlocks(headerFooter.Blocks, blockId);

        return removed
            ? (headerFooter with { Blocks = blocks }, true)
            : (headerFooter, false);
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
