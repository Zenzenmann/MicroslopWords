using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public sealed class FormatSelectionCommand(
    Selection selection,
    Func<RunProperties, RunProperties?> transform) : DocumentEditCommand
{
    public override string Description => "Format selection";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (current.IsCollapsed)
        {
            selectionAfter = current;
            return document;
        }

        var (result, mapped) = CharacterFormatting.Apply(
            document,
            current,
            transform);

        selectionAfter = mapped;
        return result;
    }
}

internal static class CharacterFormatting
{
    public static (Document Document, Selection SelectionAfter) Apply(
        Document document,
        Selection selection,
        Func<RunProperties, RunProperties?> transform)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(transform);

        var start = selection.GetStart(document);
        var end = selection.GetEnd(document);
        var startParagraphBefore = document.FindParagraph(start.ParagraphId);
        var endParagraphBefore = document.FindParagraph(end.ParagraphId);
        var startOffset = startParagraphBefore is null
            ? 0
            : TextNavigation.GetParagraphTextOffset(
                startParagraphBefore,
                start.RunId,
                start.Offset);
        var endOffset = endParagraphBefore is null
            ? 0
            : TextNavigation.GetParagraphTextOffset(
                endParagraphBefore,
                end.RunId,
                end.Offset);
        var result = document;

        foreach (var paragraph in TextNavigation.AllParagraphs(document).ToImmutableArray())
        {
            var updated = ApplyToParagraph(document, paragraph, start, end, transform);

            if (!ReferenceEquals(updated, paragraph))
            {
                result = result.ReplaceBlock(updated);
            }
        }

        var startParagraph = result.FindParagraph(start.ParagraphId);
        var endParagraph = result.FindParagraph(end.ParagraphId);
        var mappedStart = startParagraph is null
            ? selection.Anchor
            : TextNavigation.FromParagraphTextOffset(
                startParagraph,
                startOffset,
                start.Affinity);
        var mappedEnd = endParagraph is null
            ? selection.Focus
            : TextNavigation.FromParagraphTextOffset(
                endParagraph,
                endOffset,
                end.Affinity);

        return (result, new Selection(mappedStart, mappedEnd));
    }

    private static ParagraphBlock ApplyToParagraph(
        Document document,
        ParagraphBlock paragraph,
        TextPosition start,
        TextPosition end,
        Func<RunProperties, RunProperties?> transform)
    {
        var paragraphIndex = IndexOf(document, paragraph.Id);
        var startIndex = IndexOf(document, start.ParagraphId);
        var endIndex = IndexOf(document, end.ParagraphId);

        if (paragraphIndex < startIndex || paragraphIndex > endIndex)
        {
            return paragraph;
        }

        var selectionStart = paragraphIndex == startIndex
            ? TextNavigation.GetParagraphTextOffset(paragraph, start.RunId, start.Offset)
            : 0;
        var selectionEnd = paragraphIndex == endIndex
            ? TextNavigation.GetParagraphTextOffset(paragraph, end.RunId, end.Offset)
            : paragraph.PlainText.Length;

        if (selectionEnd <= selectionStart)
        {
            return paragraph;
        }

        var runs = ImmutableArray.CreateBuilder<Run>();
        var runStart = 0;
        var changed = false;

        foreach (var run in paragraph.Runs)
        {
            var runEnd = runStart + run.Text.Length;

            if (runEnd <= selectionStart || runStart >= selectionEnd)
            {
                runs.Add(run);
                runStart = runEnd;
                continue;
            }

            var from = Math.Max(selectionStart, runStart) - runStart;
            var to = Math.Min(selectionEnd, runEnd) - runStart;

            if (from == 0 && to == run.Text.Length)
            {
                var properties = Normalize(run, transform(run.EffectiveProperties));

                if (!Equals(properties, run.Properties))
                {
                    changed = true;
                }

                runs.Add(run with { Properties = properties });
            }
            else
            {
                changed = true;

                if (from > 0)
                {
                    runs.Add(run with { Text = run.Text[..from] });
                }

                runs.Add(new Run(
                    Guid.NewGuid(),
                    run.Text[from..to],
                    Normalize(run, transform(run.EffectiveProperties))));

                if (to < run.Text.Length)
                {
                    runs.Add(new Run(
                        Guid.NewGuid(),
                        run.Text[to..],
                        run.Properties));
                }
            }

            runStart = runEnd;
        }

        return changed ? paragraph with { Runs = runs.ToImmutable() } : paragraph;
    }

    private static RunProperties? Normalize(Run source, RunProperties? properties) =>
        source.Properties is null && properties == RunProperties.Default
            ? null
            : properties;
    private static int IndexOf(Document document, Guid paragraphId)
    {
        var index = 0;

        foreach (var paragraph in TextNavigation.AllParagraphs(document))
        {
            if (paragraph.Id == paragraphId)
            {
                return index;
            }

            index++;
        }

        return -1;
    }
}
