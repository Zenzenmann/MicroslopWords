using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public static class ImageSelection
{
    public static (Guid ParagraphId, Guid RunId, InlineImage Image)? FindSelectedImage(
        Document document,
        Selection selection)
    {
        ArgumentNullException.ThrowIfNull(document);
        var normalized = SelectionMap.Normalize(selection, document);

        if (normalized.IsCollapsed)
        {
            var paragraph = document.FindParagraph(normalized.Focus.ParagraphId);

            if (paragraph is null)
            {
                return null;
            }

            foreach (var run in paragraph.Runs)
            {
                if (run.Id == normalized.Focus.RunId
                    && run.EffectiveImage is { } image)
                {
                    return (paragraph.Id, run.Id, image);
                }
            }

            return null;
        }

        var start = normalized.GetStart(document);
        var end = normalized.GetEnd(document);

        foreach (var paragraph in TextNavigation.AllParagraphs(document))
        {
            if (paragraph.Id != start.ParagraphId && paragraph.Id != end.ParagraphId)
            {
                continue;
            }

            var paragraphStart = paragraph.Id == start.ParagraphId
                ? TextNavigation.GetParagraphTextOffset(paragraph, start.RunId, start.Offset)
                : 0;
            var paragraphEnd = paragraph.Id == end.ParagraphId
                ? TextNavigation.GetParagraphTextOffset(paragraph, end.RunId, end.Offset)
                : paragraph.PlainText.Length;
            var runStart = 0;

            foreach (var run in paragraph.Runs)
            {
                var runEnd = runStart + run.Text.Length;

                if (run.EffectiveImage is { } image
                    && paragraphStart <= runStart
                    && paragraphEnd >= runEnd)
                {
                    return (paragraph.Id, run.Id, image);
                }

                runStart = runEnd;
            }
        }

        return null;
    }
}

public sealed class InsertInlineImageCommand(
    Selection selection,
    InlineImage image) : DocumentEditCommand
{
    public Selection SelectionBefore { get; } = selection;

    public InlineImage Image { get; } = image;

    public override string Description => "Insert image";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(SelectionBefore, document);
        var caret = current.GetStart(document);
        var result = document;

        if (!current.IsCollapsed)
        {
            (result, caret) = TextEditOperations.DeleteRange(
                result,
                current.GetStart(document),
                current.GetEnd(document));
        }

        (result, caret) = TextEditOperations.InsertInlineImage(result, caret, Image);
        selectionAfter = Selection.Caret(caret);

        return result;
    }
}

public sealed class ResizeInlineImageCommand(
    Selection selection,
    Guid paragraphId,
    Guid runId,
    double widthPoints,
    double heightPoints,
    bool lockAspectRatio) : DocumentEditCommand
{
    public override string Description => "Resize image";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        selectionAfter = SelectionMap.Normalize(selection, document);
        var paragraph = document.FindParagraph(paragraphId);
        var runIndex = paragraph is null
            ? -1
            : TextNavigation.FindRunIndex(paragraph, runId);

        if (paragraph is null
            || runIndex < 0
            || paragraph.Runs[runIndex].EffectiveImage is not { } image)
        {
            return document;
        }

        if (!IsValidSize(widthPoints) || !IsValidSize(heightPoints))
        {
            return document;
        }

        var width = widthPoints;
        var height = lockAspectRatio
            ? widthPoints / image.AspectRatio
            : heightPoints;

        if (!IsValidSize(height))
        {
            return document;
        }

        var updated = image.WithSize(width, height) with
        {
            LockAspectRatio = lockAspectRatio,
        };
        var updatedRun = paragraph.Runs[runIndex] with { Image = updated };
        var result = document.ReplaceBlock(
            paragraph with
            {
                Runs = paragraph.Runs.SetItem(runIndex, updatedRun),
            });

        selectionAfter = SelectionMap.Normalize(selection, result);
        return result;
    }

    private static bool IsValidSize(double value) =>
        double.IsFinite(value)
        && value >= ImageValidation.MinimumSizePoints
        && value <= ImageValidation.MaximumSizePoints;
}

public sealed class SetInlineImageAltTextCommand(
    Selection selection,
    Guid paragraphId,
    Guid runId,
    string altText) : DocumentEditCommand
{
    public override string Description => "Edit image alt text";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        selectionAfter = SelectionMap.Normalize(selection, document);
        var paragraph = document.FindParagraph(paragraphId);
        var runIndex = paragraph is null
            ? -1
            : TextNavigation.FindRunIndex(paragraph, runId);

        if (paragraph is null
            || runIndex < 0
            || paragraph.Runs[runIndex].EffectiveImage is not { } image)
        {
            return document;
        }

        var updatedRun = paragraph.Runs[runIndex] with
        {
            Image = image with { AltText = altText?.Trim() ?? string.Empty },
        };
        var result = document.ReplaceBlock(
            paragraph with
            {
                Runs = paragraph.Runs.SetItem(runIndex, updatedRun),
            });

        selectionAfter = SelectionMap.Normalize(selection, result);
        return result;
    }
}

public sealed class UpdateInlineImageCommand(
    Selection selection,
    Guid paragraphId,
    Guid runId,
    double widthPoints,
    double heightPoints,
    bool lockAspectRatio,
    string altText) : DocumentEditCommand
{
    public override string Description => "Update image";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        selectionAfter = SelectionMap.Normalize(selection, document);
        var paragraph = document.FindParagraph(paragraphId);
        var runIndex = paragraph is null
            ? -1
            : TextNavigation.FindRunIndex(paragraph, runId);

        if (paragraph is null
            || runIndex < 0
            || paragraph.Runs[runIndex].EffectiveImage is not { } image)
        {
            return document;
        }

        if (!IsValidSize(widthPoints) || !IsValidSize(heightPoints))
        {
            return document;
        }

        var width = widthPoints;
        var height = lockAspectRatio
            ? widthPoints / image.AspectRatio
            : heightPoints;

        if (!IsValidSize(height))
        {
            return document;
        }

        var updated = image.WithSize(width, height) with
        {
            LockAspectRatio = lockAspectRatio,
            AltText = altText?.Trim() ?? string.Empty,
        };
        var updatedRun = paragraph.Runs[runIndex] with { Image = updated };
        var result = document.ReplaceBlock(
            paragraph with
            {
                Runs = paragraph.Runs.SetItem(runIndex, updatedRun),
            });

        selectionAfter = SelectionMap.Normalize(selection, result);
        return result;
    }

    private static bool IsValidSize(double value) =>
        double.IsFinite(value)
        && value >= ImageValidation.MinimumSizePoints
        && value <= ImageValidation.MaximumSizePoints;
}

public sealed class DeleteInlineImageCommand(
    Selection selection,
    Guid paragraphId,
    Guid runId) : DocumentEditCommand
{
    public override string Description => "Delete image";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var paragraph = document.FindParagraph(paragraphId);
        var runIndex = paragraph is null
            ? -1
            : TextNavigation.FindRunIndex(paragraph, runId);

        if (paragraph is null
            || runIndex < 0
            || paragraph.Runs[runIndex].EffectiveImage is null)
        {
            selectionAfter = SelectionMap.Normalize(selection, document);
            return document;
        }

        var runs = paragraph.Runs.RemoveAt(runIndex);
        var caret = runs.Length == 0
            ? new TextPosition(paragraph.Id, Guid.Empty, 0)
            : runIndex < runs.Length
                ? new TextPosition(paragraph.Id, runs[runIndex].Id, 0)
                : new TextPosition(
                    paragraph.Id,
                    runs[^1].Id,
                    runs[^1].Text.Length,
                    TextAffinity.Upstream);
        var result = document.ReplaceBlock(paragraph with { Runs = runs });

        selectionAfter = SelectionMap.Normalize(Selection.Caret(caret), result);
        return result;
    }
}
