using System.Collections.Immutable;
using MicroslopWords.Core.Editing;

namespace MicroslopWords.Core.Documents;

public sealed record RunFragment(
    string Text,
    RunProperties? Properties,
    InlineImage? Image = null)
{
    public string PlainText =>
        Image is null
            ? Text
            : Text.Replace(InlineImage.PlaceholderCharacter.ToString(), string.Empty, StringComparison.Ordinal);
}

public sealed record ParagraphFragment(
    ParagraphProperties? Properties,
    ImmutableArray<RunFragment> Runs)
{
    public string PlainText => string.Concat(Runs.Select(run => run.PlainText));
}

public sealed record DocumentFragment(ImmutableArray<ParagraphFragment> Paragraphs)
{
    public ImmutableArray<TableBlock> Tables { get; init; } = [];

    public static DocumentFragment Empty { get; } = new([]);

    public bool IsEmpty => Paragraphs.Length == 0 && Tables.Length == 0;

    public string PlainText =>
        string.Join(
            "\n",
            Paragraphs
                .Select(paragraph => paragraph.PlainText)
                .Concat(Tables.Select(table => table.PlainText)));

    public static DocumentFragment Capture(Document document, Selection selection)
    {
        ArgumentNullException.ThrowIfNull(document);

        var normalized = SelectionMap.Normalize(selection, document);

        if (normalized.IsCollapsed)
        {
            return Empty;
        }

        var start = normalized.GetStart(document);
        var end = normalized.GetEnd(document);
        var startTable = document.FindTableForParagraph(start.ParagraphId);
        var endTable = document.FindTableForParagraph(end.ParagraphId);

        if (startTable is not null
            && endTable is not null
            && startTable.Id == endTable.Id
            && start.ParagraphId != end.ParagraphId)
        {
            return new DocumentFragment([]) { Tables = [startTable.CloneWithNewIds()] };
        }

        var paragraphs = CaptureParagraphs(document, start, end);

        return paragraphs.Length == 0 ? Empty : new DocumentFragment(paragraphs);
    }

    public string ToPlainText() => PlainText;

    private static ImmutableArray<ParagraphFragment> CaptureParagraphs(
        Document document,
        TextPosition start,
        TextPosition end)
    {
        var paragraphs = document.EnumerateParagraphs().ToImmutableArray();
        var startIndex = FindParagraphIndex(paragraphs, start.ParagraphId);
        var endIndex = FindParagraphIndex(paragraphs, end.ParagraphId);

        if (startIndex < 0 || endIndex < 0)
        {
            return [];
        }

        if (startIndex > endIndex)
        {
            (startIndex, endIndex) = (endIndex, startIndex);
            (start, end) = (end, start);
        }

        var captured = ImmutableArray.CreateBuilder<ParagraphFragment>();

        for (var index = startIndex; index <= endIndex; index++)
        {
            var paragraph = paragraphs[index];
            TextPosition? startOffset = index == startIndex ? start : null;
            TextPosition? endOffset = index == endIndex ? end : null;

            captured.Add(CaptureParagraph(paragraph, startOffset, endOffset));
        }

        return captured.ToImmutable();
    }

    private static ParagraphFragment CaptureParagraph(
        ParagraphBlock paragraph,
        TextPosition? start,
        TextPosition? end)
    {
        if (paragraph.Runs.Length == 0)
        {
            return new ParagraphFragment(paragraph.Properties, []);
        }

        var startRunId = start?.RunId;
        var startOffset = start?.Offset ?? 0;
        var endRunId = end?.RunId;
        var endOffset = end?.Offset;
        var runs = ImmutableArray.CreateBuilder<RunFragment>();
        var started = start is null;
        var finished = false;

        foreach (var run in paragraph.Runs)
        {
            if (finished)
            {
                break;
            }

            var from = 0;
            var to = run.Text.Length;

            if (!started)
            {
                if (run.Id != startRunId)
                {
                    continue;
                }

                started = true;
                from = Math.Clamp(startOffset, 0, run.Text.Length);
            }

            if (endRunId is not null && run.Id == endRunId)
            {
                to = Math.Clamp(endOffset ?? run.Text.Length, 0, run.Text.Length);
                finished = true;
            }

            if (to > from || (startRunId is null && !finished))
            {
                var capturedText = run.Text[from..to];
                var capturedImage = run.Image is not null
                    && from == 0
                    && to == run.Text.Length
                        ? run.Image
                        : null;

                runs.Add(new RunFragment(
                    capturedText,
                    run.Properties,
                    capturedImage));
            }

        }

        return new ParagraphFragment(paragraph.Properties, runs.ToImmutable());
    }

    private static int FindParagraphIndex(
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
}
