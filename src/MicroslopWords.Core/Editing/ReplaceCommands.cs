using System.Collections.Immutable;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public sealed class ReplaceMatchCommand : DocumentEditCommand
{
    private readonly Selection _selection;
    private readonly SearchMatch _match;
    private readonly string _replacement;

    public ReplaceMatchCommand(
        Selection selection,
        SearchMatch match,
        string replacement)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(replacement);

        _selection = selection;
        _match = match;
        _replacement = replacement;
    }

    public override string Description => "Replace";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(_selection, document);
        var paragraph = document.FindParagraph(_match.ParagraphId);

        if (paragraph is null
            || _match.StartOffset < 0
            || _match.StartOffset >= paragraph.PlainText.Length
            || _match.EndOffset > paragraph.PlainText.Length)
        {
            selectionAfter = current;
            return document;
        }

        var ranges = ImmutableArray.Create(
            new ReplaceRange(_match.StartOffset, _match.EndOffset));
        var updated = ReplaceOperations.ReplaceRanges(
            paragraph,
            ranges,
            NormalizeReplacement(_replacement),
            out var caretOffset);

        selectionAfter = Selection.Caret(
            TextNavigation.FromParagraphTextOffset(updated, caretOffset));

        return document.ReplaceBlock(updated);
    }

    private static string NormalizeReplacement(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}

public sealed class ReplaceAllCommand : DocumentEditCommand
{
    private readonly Selection _selection;
    private readonly string _query;
    private readonly string _replacement;
    private readonly FindOptions _options;

    public ReplaceAllCommand(
        Selection selection,
        string query,
        string replacement,
        FindOptions options = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(replacement);

        _selection = selection;
        _query = query;
        _replacement = replacement;
        _options = options;
    }

    public override string Description => "Replace all";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(_selection, document);
        var matches = SearchEngine.Find(document, _query, _options);

        if (matches.Length == 0)
        {
            selectionAfter = current;
            return document;
        }

        var normalizedReplacement = NormalizeReplacement(_replacement);
        var matchesByParagraph = matches
            .GroupBy(match => match.ParagraphId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(match => match.StartOffset)
                    .Select(match => new ReplaceRange(
                        match.StartOffset,
                        match.EndOffset))
                    .ToImmutableArray());
        var result = document;
        TextPosition? caret = null;

        foreach (var paragraph in SearchEngine.Paragraphs(document))
        {
            if (!matchesByParagraph.TryGetValue(
                paragraph.Id,
                out var ranges))
            {
                continue;
            }

            var updated = ReplaceOperations.ReplaceRanges(
                paragraph,
                ranges,
                normalizedReplacement,
                out var caretOffset);

            caret = TextNavigation.FromParagraphTextOffset(updated, caretOffset);
            result = result.ReplaceBlock(updated);
        }

        selectionAfter = caret is { } position
            ? Selection.Caret(position)
            : current;

        return result;
    }

    private static string NormalizeReplacement(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
}

internal readonly record struct ReplaceRange(int Start, int End);

internal static class ReplaceOperations
{
    public static ParagraphBlock ReplaceRanges(
        ParagraphBlock paragraph,
        ImmutableArray<ReplaceRange> ranges,
        string replacement,
        out int caretOffset)
    {
        ArgumentNullException.ThrowIfNull(paragraph);

        if (ranges.Length == 0)
        {
            caretOffset = 0;
            return paragraph;
        }

        var runs = ImmutableArray.CreateBuilder<Run>(
            paragraph.Runs.Length + ranges.Length);
        var cursor = 0;
        var rangeIndex = 0;
        caretOffset = ranges[^1].Start + replacement.Length;

        foreach (var run in paragraph.Runs)
        {
            var runStart = cursor;
            var runEnd = runStart + run.Text.Length;
            var local = 0;
            var keptRunId = false;

            while (rangeIndex < ranges.Length
                && ranges[rangeIndex].End <= runStart)
            {
                rangeIndex++;
            }

            var overlaps = rangeIndex < ranges.Length
                && ranges[rangeIndex].Start < runEnd
                && ranges[rangeIndex].End > runStart;

            if (!overlaps)
            {
                runs.Add(run);
                cursor = runEnd;
                continue;
            }

            while (local < run.Text.Length && rangeIndex < ranges.Length)
            {
                var range = ranges[rangeIndex];

                if (range.Start >= runEnd)
                {
                    break;
                }

                var overlapStart = Math.Max(range.Start, runStart);
                var overlapEnd = Math.Min(range.End, runEnd);

                if (overlapStart > runStart + local)
                {
                    var text = run.Text[local..(overlapStart - runStart)];
                    runs.Add(Segment(run, text, keepId: !keptRunId));
                    keptRunId = true;
                }

                if (range.Start >= runStart && range.Start < runEnd
                    && replacement.Length > 0)
                {
                    runs.Add(new Run(
                        Guid.NewGuid(),
                        replacement,
                        PropertiesAt(paragraph, range.Start)));
                }

                local = overlapEnd - runStart;

                if (range.End <= runEnd)
                {
                    rangeIndex++;
                }
                else
                {
                    break;
                }
            }

            if (local < run.Text.Length)
            {
                runs.Add(Segment(run, run.Text[local..], keepId: !keptRunId));
            }

            cursor = runEnd;
        }

        return paragraph with { Runs = runs.ToImmutable() };
    }

    private static Run Segment(Run source, string text, bool keepId) =>
        new(
            keepId ? source.Id : Guid.NewGuid(),
            text,
            source.Properties,
            text == source.Text ? source.Image : null,
            text == source.Text ? source.PageField : null);

    private static RunProperties? PropertiesAt(
        ParagraphBlock paragraph,
        int offset)
    {
        var position = 0;

        foreach (var run in paragraph.Runs)
        {
            var length = run.Text.Length;

            if (offset < position + length)
            {
                return run.Properties;
            }

            position += length;
        }

        return paragraph.Runs.Length == 0
            ? null
            : paragraph.Runs[^1].Properties;
    }
}
