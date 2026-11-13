using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public enum TextAffinity
{
    Downstream,
    Upstream,
}

public readonly record struct TextPosition(
    Guid ParagraphId,
    Guid RunId,
    int Offset,
    TextAffinity Affinity = TextAffinity.Downstream);

public readonly record struct Selection(TextPosition Anchor, TextPosition Focus)
{
    public bool IsCollapsed =>
        Anchor.ParagraphId == Focus.ParagraphId
        && Anchor.RunId == Focus.RunId
        && Anchor.Offset == Focus.Offset;

    public static Selection Caret(TextPosition position) => new(position, position);

    public static Selection CaretAt(Document document, TextPosition position) =>
        Caret(TextNavigation.Normalize(document, position));

    public static Selection StartOf(Document document) =>
        Caret(TextNavigation.StartOf(FirstParagraph(document)));

    public static Selection EndOf(Document document) =>
        Caret(TextNavigation.EndOf(LastParagraph(document)));

    public TextPosition GetStart(Document document) =>
        TextNavigation.Compare(document, Anchor, Focus) <= 0 ? Anchor : Focus;

    public TextPosition GetEnd(Document document) =>
        TextNavigation.Compare(document, Anchor, Focus) <= 0 ? Focus : Anchor;

    internal static ParagraphBlock FirstParagraph(Document document) =>
        document.EnumerateParagraphs().First();

    internal static ParagraphBlock LastParagraph(Document document) =>
        document.EnumerateParagraphs().Last();
}

public readonly record struct SelectionMap(Selection Before, Selection After)
{
    public static Selection Normalize(Selection selection, Document document) =>
        new(
            TextNavigation.Normalize(document, selection.Anchor),
            TextNavigation.Normalize(document, selection.Focus));

    public Selection Map(Selection selection) =>
        selection == Before ? After : selection;
}

public interface ISelectionEditCommand : IEditCommand
{
    Selection SelectionAfter { get; }
}

public static class TextNavigation
{
    public static TextPosition Normalize(Document document, TextPosition position)
    {
        ArgumentNullException.ThrowIfNull(document);

        var paragraph = document.FindParagraph(position.ParagraphId);

        if (paragraph is null)
        {
            var fallback = position.Affinity == TextAffinity.Upstream
                ? Selection.LastParagraph(document)
                : Selection.FirstParagraph(document);

            return position.Affinity == TextAffinity.Upstream
                ? EndOf(fallback)
                : StartOf(fallback);
        }

        return Normalize(paragraph, position);
    }

    public static TextPosition Normalize(ParagraphBlock paragraph, TextPosition position)
    {
        if (paragraph.Runs.Length == 0)
        {
            return new TextPosition(paragraph.Id, Guid.Empty, 0, position.Affinity);
        }

        var runIndex = FindRunIndex(paragraph, position.RunId);

        if (runIndex < 0)
        {
            return StartOf(paragraph);
        }

        var run = paragraph.Runs[runIndex];
        var offset = Math.Clamp(position.Offset, 0, run.Text.Length);
        offset = GraphemeText.SnapToBoundary(run.Text, offset, position.Affinity);

        return new TextPosition(paragraph.Id, run.Id, offset, position.Affinity);
    }

    public static TextPosition StartOf(ParagraphBlock paragraph) =>
        new(
            paragraph.Id,
            paragraph.Runs.Length == 0 ? Guid.Empty : paragraph.Runs[0].Id,
            0);

    public static TextPosition EndOf(ParagraphBlock paragraph) =>
        paragraph.Runs.Length == 0
            ? new TextPosition(paragraph.Id, Guid.Empty, 0)
            : new TextPosition(
                paragraph.Id,
                paragraph.Runs[^1].Id,
                paragraph.Runs[^1].Text.Length,
                TextAffinity.Upstream);

    public static int Compare(Document document, TextPosition left, TextPosition right)
    {
        ArgumentNullException.ThrowIfNull(document);

        var leftParagraph = document.FindParagraph(left.ParagraphId);
        var rightParagraph = document.FindParagraph(right.ParagraphId);

        if (leftParagraph is null || rightParagraph is null)
        {
            return left.ParagraphId == right.ParagraphId
                ? left.Offset.CompareTo(right.Offset)
                : left.ParagraphId.CompareTo(right.ParagraphId);
        }

        if (leftParagraph.Id == rightParagraph.Id)
        {
            var leftRun = FindRunIndex(leftParagraph, left.RunId);
            var rightRun = FindRunIndex(rightParagraph, right.RunId);

            if (leftRun != rightRun)
            {
                return leftRun.CompareTo(rightRun);
            }

            return left.Offset.CompareTo(right.Offset);
        }

        var leftIndex = FindParagraphIndex(document, leftParagraph.Id);
        var rightIndex = FindParagraphIndex(document, rightParagraph.Id);

        return leftIndex.CompareTo(rightIndex);
    }

    public static int GetParagraphTextOffset(
        ParagraphBlock paragraph,
        Guid runId,
        int offset)
    {
        if (paragraph.Runs.Length == 0)
        {
            return 0;
        }

        var total = 0;

        foreach (var run in paragraph.Runs)
        {
            if (run.Id == runId)
            {
                return total + Math.Clamp(offset, 0, run.Text.Length);
            }

            total += run.Text.Length;
        }

        return total;
    }

    public static TextPosition FromParagraphTextOffset(
        ParagraphBlock paragraph,
        int offset,
        TextAffinity affinity = TextAffinity.Downstream)
    {
        if (paragraph.Runs.Length == 0)
        {
            return new TextPosition(paragraph.Id, Guid.Empty, 0, affinity);
        }

        offset = Math.Clamp(
            offset,
            0,
            paragraph.Runs.Sum(run => run.Text.Length));

        var remaining = offset;

        foreach (var run in paragraph.Runs)
        {
            if (remaining <= run.Text.Length)
            {
                var runOffset = GraphemeText.SnapToBoundary(
                    run.Text,
                    remaining,
                    affinity);

                return new TextPosition(paragraph.Id, run.Id, runOffset, affinity);
            }

            remaining -= run.Text.Length;
        }

        var last = paragraph.Runs[^1];

        return new TextPosition(
            paragraph.Id,
            last.Id,
            last.Text.Length,
            TextAffinity.Upstream);
    }

    public static TextPosition PreviousGrapheme(Document document, TextPosition position)
    {
        position = Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)!;
        var runIndex = FindRunIndex(paragraph, position.RunId);

        if (runIndex < 0)
        {
            return StartOf(paragraph);
        }

        var run = paragraph.Runs[runIndex];

        if (position.Offset > 0)
        {
            return new TextPosition(
                paragraph.Id,
                run.Id,
                GraphemeText.PreviousBoundary(run.Text, position.Offset));
        }

        if (runIndex > 0)
        {
            var previous = paragraph.Runs[runIndex - 1];

            return new TextPosition(
                paragraph.Id,
                previous.Id,
                previous.Text.Length,
                TextAffinity.Upstream);
        }

        var previousParagraph = PreviousParagraph(document, paragraph.Id);

        return previousParagraph is null
            ? StartOf(paragraph)
            : EndOf(previousParagraph);
    }

    public static TextPosition NextGrapheme(Document document, TextPosition position)
    {
        position = Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)!;
        var runIndex = FindRunIndex(paragraph, position.RunId);

        if (runIndex < 0)
        {
            return StartOf(paragraph);
        }

        var run = paragraph.Runs[runIndex];

        if (position.Offset < run.Text.Length)
        {
            return new TextPosition(
                paragraph.Id,
                run.Id,
                GraphemeText.NextBoundary(run.Text, position.Offset));
        }

        if (runIndex + 1 < paragraph.Runs.Length)
        {
            var next = paragraph.Runs[runIndex + 1];

            return new TextPosition(paragraph.Id, next.Id, 0);
        }

        var nextParagraph = NextParagraph(document, paragraph.Id);

        return nextParagraph is null
            ? EndOf(paragraph)
            : StartOf(nextParagraph);
    }

    public static TextPosition PreviousWord(Document document, TextPosition position)
    {
        position = Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)!;

        if (paragraph.Runs.Length == 0)
        {
            var previousEmpty = PreviousParagraph(document, paragraph.Id);

            return previousEmpty is null
                ? StartOf(paragraph)
                : EndOf(previousEmpty);
        }

        var offset = GetParagraphTextOffset(paragraph, position.RunId, position.Offset);

        if (offset == 0)
        {
            var previous = PreviousParagraph(document, paragraph.Id);

            return previous is null ? StartOf(paragraph) : EndOf(previous);
        }

        var boundary = GraphemeText.PreviousWordBoundary(paragraph.PlainText, offset);

        return FromParagraphTextOffset(paragraph, boundary, TextAffinity.Upstream);
    }

    public static TextPosition NextWord(Document document, TextPosition position)
    {
        position = Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)!;

        if (paragraph.Runs.Length == 0)
        {
            var nextEmpty = NextParagraph(document, paragraph.Id);

            return nextEmpty is null
                ? EndOf(paragraph)
                : StartOf(nextEmpty);
        }

        var offset = GetParagraphTextOffset(paragraph, position.RunId, position.Offset);

        if (offset >= paragraph.PlainText.Length)
        {
            var next = NextParagraph(document, paragraph.Id);

            return next is null ? EndOf(paragraph) : StartOf(next);
        }

        var boundary = GraphemeText.NextWordBoundary(paragraph.PlainText, offset);

        return FromParagraphTextOffset(paragraph, boundary);
    }

    public static Selection GetWordSelection(Document document, TextPosition position)
    {
        position = Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)!;
        var text = paragraph.PlainText;

        if (text.Length == 0)
        {
            return Selection.Caret(position);
        }

        var offset = GetParagraphTextOffset(paragraph, position.RunId, position.Offset);
        var starts = StringInfo.ParseCombiningCharacters(text);
        var search = Array.BinarySearch(starts, offset);
        var graphemeIndex = search >= 0
            ? search
            : Math.Max(0, ~search - 1);

        if (graphemeIndex >= starts.Length)
        {
            graphemeIndex = starts.Length - 1;
        }

        var startIndex = graphemeIndex;
        var endIndex = graphemeIndex;

        if (GraphemeText.IsWord(text, starts[graphemeIndex]))
        {
            while (startIndex > 0 && GraphemeText.IsWord(text, starts[startIndex - 1]))
            {
                startIndex--;
            }

            while (endIndex + 1 < starts.Length
                && GraphemeText.IsWord(text, starts[endIndex + 1]))
            {
                endIndex++;
            }
        }

        var start = FromParagraphTextOffset(paragraph, starts[startIndex]);
        var end = FromParagraphTextOffset(
            paragraph,
            endIndex + 1 < starts.Length ? starts[endIndex + 1] : text.Length,
            TextAffinity.Upstream);

        return new Selection(start, end);
    }

    public static Selection GetParagraphSelection(Document document, TextPosition position)
    {
        position = Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)!;
        var start = StartOf(paragraph);
        var end = FromParagraphTextOffset(
            paragraph,
            paragraph.PlainText.Length,
            TextAffinity.Upstream);

        return new Selection(start, end);
    }
    public static bool IsAtParagraphStart(Document document, TextPosition position)
    {
        position = Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)!;

        if (paragraph.Runs.Length == 0)
        {
            return true;
        }

        return position.RunId == paragraph.Runs[0].Id && position.Offset == 0;
    }

    public static bool IsAtParagraphEnd(Document document, TextPosition position)
    {
        position = Normalize(document, position);
        var paragraph = document.FindParagraph(position.ParagraphId)!;

        if (paragraph.Runs.Length == 0)
        {
            return true;
        }

        return position.RunId == paragraph.Runs[^1].Id
            && position.Offset == paragraph.Runs[^1].Text.Length;
    }

    public static ParagraphBlock? PreviousParagraph(Document document, Guid paragraphId)
    {
        var paragraphs = AllParagraphs(document).ToImmutableArray();
        var index = FindParagraphOffset(paragraphs, paragraphId);

        return index > 0
            && AreInSameCell(document, paragraphs[index - 1].Id, paragraphId)
                ? paragraphs[index - 1]
                : null;
    }

    public static ParagraphBlock? NextParagraph(Document document, Guid paragraphId)
    {
        var paragraphs = AllParagraphs(document).ToImmutableArray();
        var index = FindParagraphOffset(paragraphs, paragraphId);

        return index >= 0
            && index + 1 < paragraphs.Length
            && AreInSameCell(document, paragraphId, paragraphs[index + 1].Id)
                ? paragraphs[index + 1]
                : null;
    }

    public static bool AreInSameCell(
        Document document,
        Guid firstParagraphId,
        Guid secondParagraphId)
    {
        var firstZone = document.FindHeaderFooterZone(firstParagraphId);
        var secondZone = document.FindHeaderFooterZone(secondParagraphId);

        if (firstZone is not null || secondZone is not null)
        {
            return firstZone == secondZone;
        }

        var firstTable = document.FindTableForParagraph(firstParagraphId);
        var secondTable = document.FindTableForParagraph(secondParagraphId);

        if (firstTable is null && secondTable is null)
        {
            return true;
        }

        if (firstTable is null
            || secondTable is null
            || firstTable.Id != secondTable.Id)
        {
            return false;
        }

        var firstCell = firstTable.FindCellForParagraph(firstParagraphId);
        var secondCell = secondTable.FindCellForParagraph(secondParagraphId);

        return firstCell is not null
            && secondCell is not null
            && firstCell.Value.Cell.Id == secondCell.Value.Cell.Id;
    }

    private static int FindParagraphOffset(
        ImmutableArray<ParagraphBlock> paragraphs,
        Guid paragraphId)
    {
        for (var index = 0; index < paragraphs.Length; index++)
        {
            if (paragraphs[index].Id == paragraphId)
            {
                return index;
            }
        }

        return -1;
    }

    public static IEnumerable<ParagraphBlock> AllParagraphs(Document document) =>
        document.EnumerateAllParagraphs();

    public static int FindRunIndex(ParagraphBlock paragraph, Guid runId)
    {
        for (var index = 0; index < paragraph.Runs.Length; index++)
        {
            if (paragraph.Runs[index].Id == runId)
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindParagraphIndex(Document document, Guid paragraphId)
    {
        var index = 0;

        foreach (var paragraph in AllParagraphs(document))
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

internal static class GraphemeText
{
    public static int SnapToBoundary(
        string text,
        int offset,
        TextAffinity affinity = TextAffinity.Downstream)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        offset = Math.Clamp(offset, 0, text.Length);

        if (offset == text.Length)
        {
            return text.Length;
        }

        var starts = StringInfo.ParseCombiningCharacters(text);

        foreach (var start in starts)
        {
            if (start == offset)
            {
                return offset;
            }
        }

        var previous = 0;
        var next = text.Length;

        foreach (var start in starts)
        {
            if (start < offset)
            {
                previous = start;
            }
            else
            {
                next = start;
                break;
            }
        }

        return affinity == TextAffinity.Upstream ? next : previous;
    }

    public static int PreviousBoundary(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var starts = StringInfo.ParseCombiningCharacters(text);
        var previous = 0;

        foreach (var start in starts)
        {
            if (start < offset)
            {
                previous = start;
            }
            else
            {
                break;
            }
        }

        return previous;
    }

    public static int NextBoundary(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var starts = StringInfo.ParseCombiningCharacters(text);

        foreach (var start in starts)
        {
            if (start > offset)
            {
                return start;
            }
        }

        return text.Length;
    }

    public static int PreviousWordBoundary(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var starts = StringInfo.ParseCombiningCharacters(text);
        var index = Array.BinarySearch(starts, offset);

        if (index < 0)
        {
            index = ~index;

            if (index > 0 && starts[index - 1] < offset)
            {
                index--;
            }
        }

        while (index > 0 && IsWhitespace(text, starts[index - 1]))
        {
            index--;
        }

        if (index > 0 && IsWord(text, starts[index - 1]))
        {
            while (index > 0 && IsWord(text, starts[index - 1]))
            {
                index--;
            }
        }
        else if (index > 0)
        {
            index--;
        }

        return index < starts.Length ? starts[index] : text.Length;
    }

    public static int NextWordBoundary(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        var starts = StringInfo.ParseCombiningCharacters(text);
        var index = Array.BinarySearch(starts, offset);

        if (index < 0)
        {
            index = ~index;
        }

        while (index < starts.Length && IsWhitespace(text, starts[index]))
        {
            index++;
        }

        if (index < starts.Length && IsWord(text, starts[index]))
        {
            while (index < starts.Length && IsWord(text, starts[index]))
            {
                index++;
            }

            while (index < starts.Length && IsWhitespace(text, starts[index]))
            {
                index++;
            }
        }
        else if (index < starts.Length)
        {
            index++;
        }

        return index < starts.Length ? starts[index] : text.Length;
    }

    public static bool IsWhitespace(string text, int index)
    {
        var rune = Rune.GetRuneAt(text, index);
        return Rune.IsWhiteSpace(rune);
    }

    public static bool IsWord(string text, int index)
    {
        var rune = Rune.GetRuneAt(text, index);

        return Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber or
            UnicodeCategory.ConnectorPunctuation;
    }
}
