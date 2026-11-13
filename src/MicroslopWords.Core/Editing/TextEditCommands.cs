using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public abstract class DocumentEditCommand : ISelectionEditCommand
{
    private Document? _before;
    private Selection _selectionAfter;

    public abstract string Description { get; }

    public Selection SelectionAfter => _selectionAfter;

    public Document Apply(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        _before = document;
        var after = ApplyCore(document, out var selectionAfter);
        _selectionAfter = SelectionMap.Normalize(selectionAfter, after);

        return after;
    }

    public Document Revert(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return _before
            ?? throw new InvalidOperationException("The command has not been applied.");
    }

    protected abstract Document ApplyCore(
        Document document,
        out Selection selectionAfter);
}

public sealed class InsertTextAtSelectionCommand(
    Selection selection,
    string text,
    RunProperties? formatting = null) : DocumentEditCommand
{
    public Selection SelectionBefore { get; } = selection;

    public string Text { get; } = text;

    public RunProperties? Formatting { get; } = formatting;

    public bool CanCoalesce =>
        SelectionBefore.IsCollapsed
        && Text.Length > 0
        && !Text.Contains('\n');

    public override string Description => $"Insert \"{Text}\"";

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

        (result, caret) = Formatting is null
            ? TextEditOperations.InsertText(result, caret, Text)
            : TextEditOperations.InsertTextWithProperties(
                result,
                caret,
                Text,
                Formatting);
        selectionAfter = Selection.Caret(caret);

        return result;
    }
}

public sealed class DeleteSelectionCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Delete selection";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (current.IsCollapsed)
        {
            selectionAfter = Selection.Caret(current.GetStart(document));
            return document;
        }

        var (result, caret) = TextEditOperations.DeleteRange(
            document,
            current.GetStart(document),
            current.GetEnd(document));

        selectionAfter = Selection.Caret(caret);
        return result;
    }
}

public sealed class DeleteBackwardCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Delete backward";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!current.IsCollapsed)
        {
            var (deleted, deletedCaret) = TextEditOperations.DeleteRange(
                document,
                current.GetStart(document),
                current.GetEnd(document));

            selectionAfter = Selection.Caret(deletedCaret);
            return deleted;
        }

        var caret = current.Focus;

        if (TextNavigation.IsAtParagraphStart(document, caret))
        {
            var previous = TextNavigation.PreviousParagraph(document, caret.ParagraphId);

            if (previous is null)
            {
                selectionAfter = current;
                return document;
            }

            var (merged, mergeCaret) = TextEditOperations.MergeWithPrevious(
                document,
                caret.ParagraphId);

            selectionAfter = Selection.Caret(mergeCaret);
            return merged;
        }

        var (result, resultCaret) = TextEditOperations.DeleteRange(
            document,
            TextNavigation.PreviousGrapheme(document, caret),
            caret);

        selectionAfter = Selection.Caret(resultCaret);
        return result;
    }
}

public sealed class DeleteForwardCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Delete forward";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!current.IsCollapsed)
        {
            var (deleted, deletedCaret) = TextEditOperations.DeleteRange(
                document,
                current.GetStart(document),
                current.GetEnd(document));

            selectionAfter = Selection.Caret(deletedCaret);
            return deleted;
        }

        var caret = current.Focus;

        if (TextNavigation.IsAtParagraphEnd(document, caret))
        {
            var next = TextNavigation.NextParagraph(document, caret.ParagraphId);

            if (next is null)
            {
                selectionAfter = current;
                return document;
            }

            var (merged, mergeCaret) = TextEditOperations.MergeWithNext(
                document,
                caret.ParagraphId);

            selectionAfter = Selection.Caret(mergeCaret);
            return merged;
        }

        var (result, resultCaret) = TextEditOperations.DeleteRange(
            document,
            caret,
            TextNavigation.NextGrapheme(document, caret));

        selectionAfter = Selection.Caret(resultCaret);
        return result;
    }
}

public sealed class DeleteWordBackwardCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Delete word backward";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!current.IsCollapsed)
        {
            var (deleted, deletedCaret) = TextEditOperations.DeleteRange(
                document,
                current.GetStart(document),
                current.GetEnd(document));

            selectionAfter = Selection.Caret(deletedCaret);
            return deleted;
        }

        var caret = current.Focus;

        if (TextNavigation.IsAtParagraphStart(document, caret))
        {
            if (TextNavigation.PreviousParagraph(document, caret.ParagraphId) is null)
            {
                selectionAfter = current;
                return document;
            }

            var (merged, mergeCaret) = TextEditOperations.MergeWithPrevious(
                document,
                caret.ParagraphId);

            selectionAfter = Selection.Caret(mergeCaret);
            return merged;
        }

        var paragraph = document.FindParagraph(caret.ParagraphId)!;
        var paragraphOffset = TextNavigation.GetParagraphTextOffset(
            paragraph,
            caret.RunId,
            caret.Offset);
        var start = TextNavigation.FromParagraphTextOffset(
            paragraph,
            GraphemeText.PreviousWordBoundary(paragraph.PlainText, paragraphOffset));

        var (result, resultCaret) = TextEditOperations.DeleteRange(
            document,
            start,
            caret);

        selectionAfter = Selection.Caret(resultCaret);
        return result;
    }
}

public sealed class DeleteWordForwardCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Delete word forward";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);

        if (!current.IsCollapsed)
        {
            var (deleted, deletedCaret) = TextEditOperations.DeleteRange(
                document,
                current.GetStart(document),
                current.GetEnd(document));

            selectionAfter = Selection.Caret(deletedCaret);
            return deleted;
        }

        var caret = current.Focus;

        if (TextNavigation.IsAtParagraphEnd(document, caret))
        {
            if (TextNavigation.NextParagraph(document, caret.ParagraphId) is null)
            {
                selectionAfter = current;
                return document;
            }

            var (merged, mergeCaret) = TextEditOperations.MergeWithNext(
                document,
                caret.ParagraphId);

            selectionAfter = Selection.Caret(mergeCaret);
            return merged;
        }

        var paragraph = document.FindParagraph(caret.ParagraphId)!;
        var paragraphOffset = TextNavigation.GetParagraphTextOffset(
            paragraph,
            caret.RunId,
            caret.Offset);
        var end = TextNavigation.FromParagraphTextOffset(
            paragraph,
            GraphemeText.NextWordBoundary(paragraph.PlainText, paragraphOffset));

        var (result, resultCaret) = TextEditOperations.DeleteRange(
            document,
            caret,
            end);

        selectionAfter = Selection.Caret(resultCaret);
        return result;
    }
}

public sealed class SplitParagraphCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Split paragraph";

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

        (result, caret) = TextEditOperations.SplitParagraph(result, caret);
        selectionAfter = Selection.Caret(caret);

        return result;
    }
}

public sealed class InsertSoftBreakCommand(Selection selection) : DocumentEditCommand
{
    public override string Description => "Insert soft line break";

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

        (result, caret) = TextEditOperations.InsertText(result, caret, "\n");
        selectionAfter = Selection.Caret(caret);

        return result;
    }
}

public enum ParagraphMergeDirection
{
    WithPrevious,
    WithNext,
}

public sealed class MergeParagraphCommand(
    Guid paragraphId,
    ParagraphMergeDirection direction) : DocumentEditCommand
{
    public override string Description =>
        direction == ParagraphMergeDirection.WithPrevious
            ? "Merge paragraph with previous"
            : "Merge paragraph with next";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var (result, caret) = direction == ParagraphMergeDirection.WithPrevious
            ? TextEditOperations.MergeWithPrevious(document, paragraphId)
            : TextEditOperations.MergeWithNext(document, paragraphId);

        selectionAfter = Selection.Caret(caret);
        return result;
    }
}
public sealed class PasteTextCommand(
    Selection selection,
    string text) : DocumentEditCommand
{
    public override string Description => "Paste text";

    protected override Document ApplyCore(
        Document document,
        out Selection selectionAfter)
    {
        var current = SelectionMap.Normalize(selection, document);
        var caret = current.GetStart(document);
        var result = document;
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        if (normalized.Length == 0)
        {
            selectionAfter = current;
            return document;
        }

        var lines = normalized.Split('\n');

        if (!current.IsCollapsed)
        {
            (result, caret) = TextEditOperations.DeleteRange(
                result,
                current.GetStart(document),
                current.GetEnd(document));
        }

        for (var index = 0; index < lines.Length; index++)
        {
            (result, caret) = TextEditOperations.InsertText(result, caret, lines[index]);

            if (index < lines.Length - 1)
            {
                (result, caret) = TextEditOperations.SplitParagraph(result, caret);
            }
        }

        selectionAfter = Selection.Caret(caret);
        return result;
    }
}

public sealed class PasteFragmentCommand(
    Selection selection,
    DocumentFragment fragment) : DocumentEditCommand
{
    public override string Description => "Paste content";

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

        if (fragment.Paragraphs.Length > 0)
        {
            (result, caret) = TextEditOperations.InsertFragment(result, caret, fragment);
        }

        if (fragment.Tables.Length > 0)
        {
            (result, caret) = TextEditOperations.InsertTables(
                result,
                caret,
                fragment.Tables);
        }

        selectionAfter = Selection.Caret(caret);

        return result;
    }
}
