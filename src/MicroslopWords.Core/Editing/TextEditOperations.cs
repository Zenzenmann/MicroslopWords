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

        if (target.EffectiveImage is not null)
        {
            var (prefix, suffix) = SplitRuns(paragraph, position);
            var inserted = new Run(Guid.NewGuid(), text, target.Properties);
            var runs = AddNonEmpty(prefix, inserted, suffix);

            return (
                document.ReplaceBlock(paragraph with { Runs = runs }),
                new TextPosition(paragraph.Id, inserted.Id, text.Length));
        }

        var updatedRun = target with { Text = target.Text.Insert(position.Offset, text) };
        var update = paragraph.Runs.SetItem(runIndex, updatedRun);

        return (
            document.ReplaceBlock(paragraph with { Runs = update }),
            new TextPosition(paragraph.Id, updatedRun.Id, position.Offset + text.Length));
    }

    public static (Document Document, TextPosition Caret) InsertTextWithProperties(
        Document document,
        TextPosition position,
        string text,
        RunProperties properties)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(properties);

        position = TextNavigation.Normalize(document, position);

        if (text.Length == 0)
        {
            return (document, position);
        }

        var paragraph = document.FindParagraph(position.ParagraphId)
            ?? throw new InvalidOperationException("The insertion paragraph was not found.");

        if (paragraph.Runs.Length == 0)
        {
            var emptyRun = new Run(Guid.NewGuid(), text, properties);

            return (
                document.ReplaceBlock(paragraph with { Runs = [emptyRun] }),
                new TextPosition(paragraph.Id, emptyRun.Id, text.Length));
        }

        var (prefix, suffix) = SplitRuns(paragraph, position);
        var run = new Run(Guid.NewGuid(), text, properties);
        var runs = prefix.Add(run).AddRange(suffix);

        return (
            document.ReplaceBlock(paragraph with { Runs = runs }),
            new TextPosition(paragraph.Id, run.Id, text.Length));
    }
    public static (Document Document, TextPosition Caret) InsertInlineImage(
        Document document,
        TextPosition position,
        InlineImage image)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(image);

        position = TextNavigation.Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)
            ?? throw new InvalidOperationException("The insertion paragraph was not found.");
        var (prefix, suffix) = SplitRuns(paragraph, position);
        var run = Run.CreateImage(image);
        var runs = AddNonEmpty(prefix, run, suffix);

        return (
            document.ReplaceBlock(paragraph with { Runs = runs }),
            new TextPosition(paragraph.Id, run.Id, 1, TextAffinity.Upstream));
    }

    public static (Document Document, TextPosition Caret) InsertFragment(
        Document document,
        TextPosition caret,
        DocumentFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(fragment);

        caret = TextNavigation.Normalize(document, caret);

        if (fragment.IsEmpty)
        {
            return (document, caret);
        }

        var paragraph = document.FindParagraph(caret.ParagraphId)
            ?? throw new InvalidOperationException("The insertion paragraph was not found.");

        var (prefix, suffix) = SplitRuns(paragraph, caret);

        if (fragment.Paragraphs.Length == 1)
        {
            var inserted = fragment.Paragraphs[0].Runs
                .Select(CloneRun)
                .ToImmutableArray();
            var runs = prefix.AddRange(inserted).AddRange(suffix);
            var updated = paragraph with
            {
                Runs = runs,
                Properties = paragraph.PlainText.Length == 0
                    ? fragment.Paragraphs[0].Properties
                    : paragraph.Properties,
            };

            return (
                document.ReplaceBlock(updated),
                CaretAfterInserted(updated, prefix, inserted));
        }

        var firstInserted = fragment.Paragraphs[0].Runs
            .Select(CloneRun)
            .ToImmutableArray();
        var first = paragraph with
        {
            Runs = prefix.AddRange(firstInserted),
            Properties = paragraph.PlainText.Length == 0
                ? fragment.Paragraphs[0].Properties
                : paragraph.Properties,
        };
        var previousId = first.Id;
        var updatedDocument = document.ReplaceBlock(first);
        ParagraphBlock? last = null;
        ImmutableArray<Run> lastInserted = [];

        for (var index = 1; index < fragment.Paragraphs.Length; index++)
        {
            var source = fragment.Paragraphs[index];
            var inserted = source.Runs.Select(CloneRun).ToImmutableArray();
            lastInserted = inserted;
            var isLast = index == fragment.Paragraphs.Length - 1;
            var runs = isLast ? inserted.AddRange(suffix) : inserted;
            var insertedParagraph = new ParagraphBlock(
                Guid.NewGuid(),
                runs,
                source.Properties);

            updatedDocument = updatedDocument.InsertBlockAfter(previousId, insertedParagraph);
            previousId = insertedParagraph.Id;
            last = insertedParagraph;
        }

        var finalCaret = last is null
            ? CaretAfterInserted(first, prefix, firstInserted)
            : CaretAfterInserted(last, [], lastInserted);

        return (updatedDocument, finalCaret);
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

                if (run.EffectiveImage is not null)
                {
                    if (offset == 0)
                    {
                        secondRuns.Add(run);
                    }
                    else
                    {
                        firstRuns.Add(run);
                    }

                    continue;
                }

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

            if (text.Length == 0 && run.EffectiveImage is not null)
            {
                var remaining = paragraph.Runs.RemoveAt(runIndex);
                var caret = remaining.Length == 0
                    ? new TextPosition(paragraph.Id, Guid.Empty, 0)
                    : runIndex < remaining.Length
                        ? new TextPosition(paragraph.Id, remaining[runIndex].Id, 0)
                        : new TextPosition(
                            paragraph.Id,
                            remaining[^1].Id,
                            remaining[^1].Text.Length,
                            TextAffinity.Upstream);

                return (
                    document.ReplaceBlock(paragraph with { Runs = remaining }),
                    caret);
            }

            var updated = paragraph.Runs.SetItem(
                runIndex,
                run with
                {
                    Text = text,
                    Image = text == run.Text ? run.Image : null,
                });

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
                var offset = Math.Clamp(start.Offset, 0, run.Text.Length);

                if (offset > 0)
                {
                    runs.Add(SliceRun(run, 0, offset));
                }

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
                var offset = Math.Clamp(end.Offset, 0, run.Text.Length);

                if (offset < run.Text.Length)
                {
                    runs.Add(SliceRun(run, offset, run.Text.Length));
                }

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

    private static (ImmutableArray<Run> Prefix, ImmutableArray<Run> Suffix) SplitRuns(
        ParagraphBlock paragraph,
        TextPosition caret)
    {
        if (paragraph.Runs.Length == 0)
        {
            return ([], []);
        }

        var runIndex = TextNavigation.FindRunIndex(paragraph, caret.RunId);

        if (runIndex < 0)
        {
            runIndex = 0;
        }

        var run = paragraph.Runs[runIndex];
        var offset = Math.Clamp(caret.Offset, 0, run.Text.Length);
        var prefix = ImmutableArray.CreateBuilder<Run>();
        var suffix = ImmutableArray.CreateBuilder<Run>();

        for (var index = 0; index < runIndex; index++)
        {
            prefix.Add(paragraph.Runs[index]);
        }

        if (offset > 0)
        {
            prefix.Add(SliceRun(run, 0, offset));
        }

        if (offset < run.Text.Length)
        {
            suffix.Add(run.Image is not null
                ? run
                : new Run(Guid.NewGuid(), run.Text[offset..], run.Properties));
        }

        for (var index = runIndex + 1; index < paragraph.Runs.Length; index++)
        {
            suffix.Add(paragraph.Runs[index]);
        }

        return (prefix.ToImmutable(), suffix.ToImmutable());
    }

    private static Run CloneRun(RunFragment fragment) =>
        new(Guid.NewGuid(), fragment.Text, fragment.Properties, fragment.Image);

    private static Run SliceRun(Run run, int from, int to)
    {
        var text = run.Text[from..to];
        var keepsImage = run.EffectiveImage is not null
            && from == 0
            && to == run.Text.Length;

        return run with
        {
            Text = text,
            Image = keepsImage ? run.Image : null,
        };
    }

    private static ImmutableArray<Run> AddNonEmpty(
        ImmutableArray<Run> prefix,
        Run run,
        ImmutableArray<Run> suffix)
    {
        var runs = ImmutableArray.CreateBuilder<Run>(prefix.Length + 1 + suffix.Length);

        foreach (var candidate in prefix)
        {
            if (candidate.Text.Length > 0 || candidate.EffectiveImage is not null)
            {
                runs.Add(candidate);
            }
        }

        runs.Add(run);

        foreach (var candidate in suffix)
        {
            if (candidate.Text.Length > 0 || candidate.EffectiveImage is not null)
            {
                runs.Add(candidate);
            }
        }

        return runs.ToImmutable();
    }

    private static TextPosition CaretAfterInserted(
        ParagraphBlock paragraph,
        ImmutableArray<Run> prefix,
        ImmutableArray<Run> inserted)
    {
        if (inserted.Length > 0)
        {
            var last = inserted[^1];

            return new TextPosition(paragraph.Id, last.Id, last.Text.Length);
        }

        if (prefix.Length > 0)
        {
            var last = prefix[^1];

            return new TextPosition(paragraph.Id, last.Id, last.Text.Length);
        }

        return TextNavigation.StartOf(paragraph);
    }
}
