using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

internal static class TextEditOperations
{
    public static (Document Document, TextPosition Caret) InsertText(
        Document document,
        TextPosition position,
        string text)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);

        position = TextNavigation.Normalize(document, position);
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        var paragraph = document.FindParagraph(position.ParagraphId)
            ?? throw new InvalidOperationException("The insertion paragraph was not found.");

        if (text.Length == 0)
        {
            return (document, position);
        }

        if (paragraph.Runs.Length == 0)
        {
            var run = new Run(Guid.NewGuid(), text);

            return (
                document.ReplaceBlock(paragraph with { Runs = [run] }),
                new TextPosition(paragraph.Id, run.Id, text.Length));
        }

        var runIndex = TextNavigation.FindRunIndex(paragraph, position.RunId);

        if (runIndex < 0)
        {
            runIndex = 0;
            position = new TextPosition(
                paragraph.Id,
                paragraph.Runs[0].Id,
                0);
        }

        var target = paragraph.Runs[runIndex];
        var updatedRun = target with { Text = target.Text.Insert(position.Offset, text) };
        var update = paragraph.Runs.SetItem(runIndex, updatedRun);

        return (
            document.ReplaceBlock(paragraph with { Runs = update }),
            new TextPosition(paragraph.Id, updatedRun.Id, position.Offset + text.Length));
    }

    public static (Document Document, TextPosition Caret) DeleteRange(
        Document document,
        TextPosition start,
        TextPosition end)
    {
        ArgumentNullException.ThrowIfNull(document);

        start = TextNavigation.Normalize(document, start);
        end = TextNavigation.Normalize(document, end);

        if (TextNavigation.Compare(document, start, end) > 0)
        {
            (start, end) = (end, start);
        }

        if (start.ParagraphId == end.ParagraphId)
        {
            return DeleteInParagraph(document, start, end);
        }

        return DeleteAcrossParagraphs(document, start, end);
    }

    public static (Document Document, TextPosition Caret) SplitParagraph(
        Document document,
        TextPosition caret)
    {
        ArgumentNullException.ThrowIfNull(document);

        caret = TextNavigation.Normalize(document, caret);
        var paragraph = document.FindParagraph(caret.ParagraphId)
            ?? throw new InvalidOperationException("The paragraph was not found.");

        var firstRuns = ImmutableArray.CreateBuilder<Run>();
        var secondRuns = ImmutableArray.CreateBuilder<Run>();
        var found = paragraph.Runs.Length == 0;
        var offset = caret.Offset;

        foreach (var run in paragraph.Runs)
        {
            if (run.Id == caret.RunId)
            {
                found = true;
                offset = Math.Clamp(offset, 0, run.Text.Length);

                firstRuns.Add(run with { Text = run.Text[..offset] });
                secondRuns.Add(run with
                {
                    Id = Guid.NewGuid(),
                    Text = run.Text[offset..],
                });

                continue;
            }

            if (found)
            {
                secondRuns.Add(run);
            }
            else
            {
                firstRuns.Add(run);
            }
        }

        var first = paragraph with { Runs = firstRuns.ToImmutable() };
        var second = new ParagraphBlock(
            Guid.NewGuid(),
            secondRuns.ToImmutable(),
            paragraph.Properties);

        var updated = document
            .ReplaceBlock(first)
            .InsertBlockAfter(first.Id, second);

        return (updated, TextNavigation.StartOf(second));
    }

    public static (Document Document, TextPosition Caret) MergeWithNext(
        Document document,
        Guid paragraphId)
    {
        ArgumentNullException.ThrowIfNull(document);

        var paragraph = document.FindParagraph(paragraphId);

        if (paragraph is null)
        {
            return (document, Selection.StartOf(document).Focus);
        }

        var next = TextNavigation.NextParagraph(document, paragraphId);

        if (next is null || !TryGetBlockLocation(document, next.Id, out var nextLocation))
        {
            return (document, TextNavigation.EndOf(paragraph));
        }

        if (!TryGetBlockLocation(document, paragraphId, out var location)
            || location.Section.Id != nextLocation.Section.Id)
        {
            return (document, TextNavigation.EndOf(paragraph));
        }

        var caret = paragraph.Runs.Length == 0
            ? new TextPosition(paragraph.Id, Guid.Empty, 0)
            : new TextPosition(
                paragraph.Id,
                paragraph.Runs[^1].Id,
                paragraph.Runs[^1].Text.Length,
                TextAffinity.Upstream);

        var merged = paragraph with { Runs = paragraph.Runs.AddRange(next.Runs) };
        var sections = document.Sections
            .Select(section => section.Id == location.Section.Id
                ? section with
                {
                    Blocks = section.Blocks
                        .SetItem(location.BlockIndex, (Block)merged)
                        .RemoveAt(nextLocation.BlockIndex),
                }
                : section)
            .ToImmutableArray();

        return (document with { Sections = sections }, caret);
    }

    public static (Document Document, TextPosition Caret) MergeWithPrevious(
        Document document,
        Guid paragraphId)
    {
        var previous = TextNavigation.PreviousParagraph(document, paragraphId);

        return previous is null
            ? (document, Selection.StartOf(document).Focus)
            : MergeWithNext(document, previous.Id);
    }

    private static (Document Document, TextPosition Caret) DeleteInParagraph(
        Document document,
        TextPosition start,
        TextPosition end)
    {
        var paragraph = document.FindParagraph(start.ParagraphId)!;

        if (paragraph.Runs.Length == 0)
        {
            return (document, start);
        }

        if (start.RunId == end.RunId)
        {
            var runIndex = TextNavigation.FindRunIndex(paragraph, start.RunId);

            if (runIndex < 0)
            {
                return (document, TextNavigation.StartOf(paragraph));
            }

            var run = paragraph.Runs[runIndex];
            var text = run.Text[..Math.Clamp(start.Offset, 0, run.Text.Length)]
                + run.Text[Math.Clamp(end.Offset, 0, run.Text.Length)..];
            var updated = paragraph.Runs.SetItem(runIndex, run with { Text = text });

            return (
                document.ReplaceBlock(paragraph with { Runs = updated }),
                new TextPosition(paragraph.Id, run.Id, start.Offset));
        }

        var runs = BuildPrefix(paragraph, start)
            .AddRange(BuildSuffix(paragraph, end));
        var merged = paragraph with { Runs = runs };

        return (
            document.ReplaceBlock(merged),
            CaretAtJoin(merged, start));
    }

    private static (Document Document, TextPosition Caret) DeleteAcrossParagraphs(
        Document document,
        TextPosition start,
        TextPosition end)
    {
        if (!TryGetBlockLocation(document, start.ParagraphId, out var startLocation)
            || !TryGetBlockLocation(document, end.ParagraphId, out var endLocation))
        {
            return (document, start);
        }

        if (startLocation.Section.Id != endLocation.Section.Id)
        {
            return (document, start);
        }

        var startParagraph = document.FindParagraph(start.ParagraphId)!;
        var endParagraph = document.FindParagraph(end.ParagraphId)!;

        var runs = BuildPrefix(startParagraph, start)
            .AddRange(BuildSuffix(endParagraph, end));
        var merged = startParagraph with { Runs = runs };
        var removeCount = endLocation.BlockIndex - startLocation.BlockIndex;

        if (removeCount <= 0)
        {
            return (document, start);
        }

        var blocks = startLocation.Section.Blocks
            .SetItem(startLocation.BlockIndex, (Block)merged)
            .RemoveRange(startLocation.BlockIndex + 1, removeCount);

        return (
            document.ReplaceSection(startLocation.Section with { Blocks = blocks }),
            CaretAtJoin(merged, start));
    }

    private static ImmutableArray<Run> BuildPrefix(
        ParagraphBlock paragraph,
        TextPosition start)
    {
        var runs = ImmutableArray.CreateBuilder<Run>();

        if (paragraph.Runs.Length == 0)
        {
            return runs.ToImmutable();
        }

        foreach (var run in paragraph.Runs)
        {
            if (run.Id == start.RunId)
            {
                runs.Add(run with
                {
                    Text = run.Text[..Math.Clamp(start.Offset, 0, run.Text.Length)],
                });
                break;
            }

            runs.Add(run);
        }

        return runs.ToImmutable();
    }

    private static ImmutableArray<Run> BuildSuffix(
        ParagraphBlock paragraph,
        TextPosition end)
    {
        var runs = ImmutableArray.CreateBuilder<Run>();
        var found = false;

        foreach (var run in paragraph.Runs)
        {
            if (!found && run.Id == end.RunId)
            {
                found = true;
                runs.Add(run with
                {
                    Text = run.Text[Math.Clamp(end.Offset, 0, run.Text.Length)..],
                });
                continue;
            }

            if (found)
            {
                runs.Add(run);
            }
        }

        return runs.ToImmutable();
    }

    private static TextPosition CaretAtJoin(ParagraphBlock paragraph, TextPosition start)
    {
        if (paragraph.Runs.Length == 0)
        {
            return new TextPosition(paragraph.Id, Guid.Empty, 0);
        }

        var startRunIndex = TextNavigation.FindRunIndex(paragraph, start.RunId);

        if (startRunIndex >= 0)
        {
            var run = paragraph.Runs[startRunIndex];

            return new TextPosition(
                paragraph.Id,
                run.Id,
                Math.Clamp(start.Offset, 0, run.Text.Length));
        }

        return TextNavigation.StartOf(paragraph);
    }

    private static bool TryGetBlockLocation(
        Document document,
        Guid blockId,
        out (Section Section, int BlockIndex) location)
    {
        foreach (var section in document.Sections)
        {
            for (var index = 0; index < section.Blocks.Length; index++)
            {
                if (section.Blocks[index].Id == blockId)
                {
                    location = (section, index);
                    return true;
                }
            }
        }

        location = default;
        return false;
    }
}
