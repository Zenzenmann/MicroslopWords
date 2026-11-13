using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public sealed class EnterHeaderFooterCommand(
    Guid sectionId,
    HeaderFooterKind kind,
    HeaderFooterVariant variant) : DocumentEditCommand
{
    public override string Description => "Enter header or footer";

    public int OwnerSectionIndex { get; private set; }

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var sectionIndex = FindSectionIndex(document, sectionId);
        var (result, ownerIndex, headerFooter) = document.EnsureHeaderFooter(
            sectionIndex,
            kind,
            variant);
        OwnerSectionIndex = ownerIndex;

        if (variant == HeaderFooterVariant.FirstPage)
        {
            var section = result.Sections[sectionIndex];

            if (!section.DifferentFirstPage)
            {
                result = result.ReplaceSection(section with
                {
                    DifferentFirstPage = true,
                });
            }
        }

        var paragraph = headerFooter.Blocks
            .SelectMany(EnumerateParagraphs)
            .FirstOrDefault()
            ?? ParagraphBlock.Create();

        selectionAfter = Selection.Caret(TextNavigation.StartOf(paragraph));
        return result;
    }

    private static int FindSectionIndex(Document document, Guid sectionId)
    {
        for (var index = 0; index < document.Sections.Length; index++)
        {
            if (document.Sections[index].Id == sectionId)
            {
                return index;
            }
        }

        return 0;
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

        foreach (var cellParagraph in table.Rows
                     .SelectMany(row => row.Cells)
                     .SelectMany(cell => cell.Paragraphs))
        {
            yield return cellParagraph;
        }
    }
}

public sealed class InsertPageFieldCommand(
    Selection selection,
    PageFieldKind kind) : DocumentEditCommand
{
    public override string Description => "Insert page number field";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var caret = current.GetStart(document);
        var result = document;

        if (!current.IsCollapsed)
        {
            (result, caret) = TextEditOperations.DeleteRange(
                result,
                current.GetStart(document),
                current.GetEnd(document));
        }

        (result, caret) = TextEditOperations.InsertPageField(result, caret, kind);
        selectionAfter = Selection.Caret(caret);

        return result;
    }
}

public sealed class SetPageNumberFormatCommand(
    Selection selection,
    Guid sectionId,
    PageNumberFormat? format) : DocumentEditCommand
{
    public override string Description => "Set page number format";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var section = document.Sections.FirstOrDefault(item => item.Id == sectionId);
        selectionAfter = SelectionMap.Normalize(selection, document);

        return section is null
            ? document
            : document.ReplaceSection(section with { PageNumberFormat = format });
    }
}

public sealed class SetPageNumberStartCommand(
    Selection selection,
    Guid sectionId,
    int? start) : DocumentEditCommand
{
    public override string Description => "Set page number start";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var section = document.Sections.FirstOrDefault(item => item.Id == sectionId);
        selectionAfter = SelectionMap.Normalize(selection, document);

        if (section is null)
        {
            return document;
        }

        if (start is < 1)
        {
            throw new InvalidOperationException(
                "The page number start must be at least 1.");
        }

        return document.ReplaceSection(section with { PageNumberStart = start });
    }
}

public sealed class SetDifferentFirstPageCommand(
    Selection selection,
    Guid sectionId,
    bool enabled) : DocumentEditCommand
{
    public override string Description => "Toggle different first page";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var section = document.Sections.FirstOrDefault(item => item.Id == sectionId);
        selectionAfter = SelectionMap.Normalize(selection, document);

        return section is null
            ? document
            : document.ReplaceSection(section with { DifferentFirstPage = enabled });
    }
}

public sealed class SetHeaderFooterLinkCommand(
    Selection selection,
    Guid sectionId,
    HeaderFooterKind kind,
    bool linked) : DocumentEditCommand
{
    public override string Description => linked
        ? "Link header or footer to previous section"
        : "Unlink header or footer from previous section";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var sectionIndex = -1;

        for (var index = 0; index < document.Sections.Length; index++)
        {
            if (document.Sections[index].Id == sectionId)
            {
                sectionIndex = index;
                break;
            }
        }

        selectionAfter = SelectionMap.Normalize(selection, document);

        if (sectionIndex < 0)
        {
            return document;
        }

        if (linked && sectionIndex == 0)
        {
            throw new InvalidOperationException(
                "The first section cannot link to a previous section.");
        }

        var effectiveDefault = document.ResolveHeaderFooter(
            sectionIndex,
            kind,
            HeaderFooterVariant.Default);
        var effectiveFirst = document.ResolveHeaderFooter(
            sectionIndex,
            kind,
            HeaderFooterVariant.FirstPage);
        var result = document.SetHeaderFooterLink(
            sectionIndex,
            kind,
            linked,
            effectiveDefault is null
                ? null
                : HeaderFooterEditing.Copy(effectiveDefault));

        if (!linked && effectiveFirst is not null)
        {
            result = result.SetHeaderFooter(
                sectionIndex,
                kind,
                HeaderFooterVariant.FirstPage,
                HeaderFooterEditing.Copy(effectiveFirst));
        }

        return result;
    }
}

internal static class HeaderFooterEditing
{
    public static HeaderFooter Copy(HeaderFooter source) =>
        new(source.Blocks
            .Select(CloneBlock)
            .ToImmutableArray());

    private static Block CloneBlock(Block block) =>
        block switch
        {
            ParagraphBlock paragraph => new ParagraphBlock(
                Guid.NewGuid(),
                paragraph.Runs
                    .Select(run => new Run(
                        Guid.NewGuid(),
                        run.Text,
                        run.Properties,
                        run.Image,
                        run.PageField))
                    .ToImmutableArray(),
                paragraph.Properties),
            TableBlock table => table.CloneWithNewIds(),
            _ => block,
        };
}
