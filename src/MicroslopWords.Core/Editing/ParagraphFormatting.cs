using System.Collections.Immutable;
using MicroslopWords.Core.Documents;
using MicroslopWords.Core.Styles;

namespace MicroslopWords.Core.Editing;

public sealed class ApplyParagraphPropertiesCommand(
    Selection selection,
    Func<ParagraphProperties, ParagraphProperties?> transform) : DocumentEditCommand
{
    public override string Description => "Format paragraphs";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var result = ParagraphFormatting.Apply(document, current, transform);

        selectionAfter = current;
        return result;
    }
}

public sealed class ToggleListCommand(
    Selection selection,
    ListKind kind) : DocumentEditCommand
{
    public override string Description =>
        kind == ListKind.Numbered ? "Toggle numbered list" : "Toggle bulleted list";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (kind == ListKind.None)
        {
            selectionAfter = current;
            return document;
        }

        var paragraphs = ParagraphFormatting.GetAffectedParagraphs(document, current);

        if (paragraphs.Count == 0)
        {
            selectionAfter = current;
            return document;
        }

        var allInKind = paragraphs.All(paragraph =>
            StyleResolver.ResolveParagraph(paragraph, document.EffectiveStyles).ListKind == kind);

        if (allInKind)
        {
            var removed = ParagraphFormatting.Apply(
                document,
                current,
                properties => properties with
                {
                    ListKind = ListKind.None,
                    ListLevel = 1,
                    ListId = null,
                });

            selectionAfter = current;
            return removed;
        }

        var existingId = paragraphs
            .Select(paragraph => StyleResolver.ResolveParagraph(paragraph, document.EffectiveStyles))
            .FirstOrDefault(properties =>
                properties.ListKind == kind && properties.ListId is not null)
            ?.ListId
            ?? Guid.NewGuid();

        var updated = ParagraphFormatting.Apply(
            document,
            current,
            properties => properties with
            {
                ListKind = kind,
                ListLevel = properties.ListKind == kind
                    ? Math.Clamp(properties.ListLevel, 1, 9)
                    : 1,
                ListId = existingId,
            });

        selectionAfter = current;
        return updated;
    }
}

public sealed class SetListLevelCommand(
    Selection selection,
    int level) : DocumentEditCommand
{
    public override string Description => "Set list level";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        if (level is < 1 or > 9)
        {
            throw new InvalidOperationException(
                "List level must be between 1 and 9.");
        }

        var current = SelectionMap.Normalize(selection, document);
        var result = ParagraphFormatting.Apply(
            document,
            current,
            properties => properties.ListKind == ListKind.None
                ? properties
                : properties with { ListLevel = level });

        selectionAfter = current;
        return result;
    }
}

public sealed class RestartNumberingCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Restart list numbering";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var paragraphs = ParagraphFormatting.GetAffectedParagraphs(document, current);

        if (paragraphs.Count == 0)
        {
            selectionAfter = current;
            return document;
        }

        var target = StyleResolver.ResolveParagraph(paragraphs[0], document.EffectiveStyles);

        if (target.ListKind != ListKind.Numbered || target.ListId is null)
        {
            selectionAfter = current;
            return document;
        }

        var all = ParagraphFormatting.GetAllParagraphs(document);
        var startIndex = ParagraphFormatting.IndexOf(all, paragraphs[0].Id);

        if (startIndex < 0)
        {
            selectionAfter = current;
            return document;
        }

        var listId = target.ListId.Value;
        var newId = Guid.NewGuid();
        var result = document;

        for (var index = startIndex; index < all.Length; index++)
        {
            var paragraph = all[index];
            var properties = StyleResolver.ResolveParagraph(paragraph, result.EffectiveStyles);

            if (properties.ListKind != ListKind.Numbered
                || properties.ListId != listId)
            {
                break;
            }

            var direct = paragraph.Properties
                ?? new ParagraphProperties { StyleId = properties.StyleId };
            result = result.ReplaceBlock(paragraph with
            {
                Properties = direct with
                {
                    ListId = newId,
                    ListKind = properties.ListKind,
                    ListLevel = properties.ListLevel,
                    Overrides = direct.Overrides | ParagraphPropertyOverrides.List,
                },
            });
        }

        selectionAfter = current;
        return result;
    }
}

public sealed class InsertPageBreakCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Insert page break";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var inner = new InsertTextAtSelectionCommand(
            selection,
            TextBreaks.PageBreak.ToString());
        var after = inner.Apply(document);

        selectionAfter = inner.SelectionAfter;
        return after;
    }
}

public sealed class ClearParagraphFormattingCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Clear paragraph formatting";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var paragraphs = ParagraphFormatting.GetAffectedParagraphs(document, current);
        var result = document;

        foreach (var paragraph in paragraphs)
        {
            var effective = StyleResolver.ResolveParagraph(paragraph, result.EffectiveStyles);
            var hasList = effective.ListKind != ListKind.None;
            ParagraphProperties? direct = null;

            if (!string.IsNullOrWhiteSpace(effective.StyleId) || hasList)
            {
                direct = new ParagraphProperties
                {
                    StyleId = effective.StyleId,
                    ListKind = effective.ListKind,
                    ListLevel = effective.ListLevel,
                    ListId = effective.ListId,
                    Overrides = hasList ? ParagraphPropertyOverrides.List : ParagraphPropertyOverrides.None,
                };
            }

            result = result.ReplaceBlock(paragraph with { Properties = direct });
        }

        selectionAfter = current;
        return result;
    }
}

internal static class ParagraphFormatting
{
    public static Document Apply(
        Document document,
        Selection selection,
        Func<ParagraphProperties, ParagraphProperties?> transform)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(transform);

        var paragraphs = GetAffectedParagraphs(document, selection);
        var result = document;

        foreach (var paragraph in paragraphs)
        {
            var effective = StyleResolver.ResolveParagraph(paragraph, result.EffectiveStyles);
            var transformed = transform(effective);

            if (transformed is null)
            {
                result = result.ReplaceBlock(paragraph with
                {
                    Properties = ParagraphFormatting.CreateClearedDirect(paragraph.Properties, effective),
                });
                continue;
            }

            if (Equals(transformed, effective))
            {
                continue;
            }

            ParagraphProperties direct;

            if (string.IsNullOrWhiteSpace(effective.StyleId))
            {
                direct = transformed;
            }
            else
            {
                var current = paragraph.Properties
                    ?? new ParagraphProperties { StyleId = effective.StyleId };
                direct = StyleResolver.MergeOverrides(current, effective, transformed);
            }

            result = result.ReplaceBlock(paragraph with { Properties = direct });
        }

        return result;
    }

    private static ParagraphProperties? CreateClearedDirect(
        ParagraphProperties? current,
        ParagraphProperties effective)
    {
        if (string.IsNullOrWhiteSpace(effective.StyleId)
            && effective.ListKind == ListKind.None)
        {
            return current is null ? null : new ParagraphProperties();
        }

        return new ParagraphProperties
        {
            StyleId = effective.StyleId,
            ListKind = effective.ListKind,
            ListLevel = effective.ListLevel,
            ListId = effective.ListId,
            Overrides = effective.ListKind == ListKind.None
                ? ParagraphPropertyOverrides.None
                : ParagraphPropertyOverrides.List,
        };
    }

    public static List<ParagraphBlock> GetAffectedParagraphs(
        Document document,
        Selection selection)
    {
        var all = GetAllParagraphs(document);

        if (all.Length == 0)
        {
            return [];
        }

        if (selection.IsCollapsed)
        {
            var paragraph = document.FindParagraph(selection.Focus.ParagraphId);
            return paragraph is null ? [] : [paragraph];
        }

        var start = selection.GetStart(document);
        var end = selection.GetEnd(document);
        var startIndex = IndexOf(all, start.ParagraphId);
        var endIndex = IndexOf(all, end.ParagraphId);

        if (startIndex < 0 || endIndex < 0)
        {
            return [];
        }

        if (startIndex > endIndex)
        {
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        return all.Skip(startIndex).Take(endIndex - startIndex + 1).ToList();
    }

    internal static int IndexOf(ImmutableArray<ParagraphBlock> paragraphs, Guid id)
    {
        for (var index = 0; index < paragraphs.Length; index++)
        {
            if (paragraphs[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

    public static ImmutableArray<ParagraphBlock> GetAllParagraphs(Document document) =>
        document.Sections
            .SelectMany(section => section.Blocks)
            .OfType<ParagraphBlock>()
            .ToImmutableArray();
}
