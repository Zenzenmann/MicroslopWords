using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public sealed class CommandHistory
{
    private readonly Stack<HistoryEntry> _undoStack = new();
    private readonly Stack<HistoryEntry> _redoStack = new();

    public CommandHistory(Document initial, Selection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(initial);

        Current = initial;
        Selection = SelectionMap.Normalize(
            selection ?? Selection.StartOf(initial),
            initial);
    }

    public Document Current { get; private set; }

    public Selection Selection { get; private set; }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public Document Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var before = Current;
        var beforeSelection = Selection;
        var after = command.Apply(Current);
        var afterSelection = command is ISelectionEditCommand selectionCommand
            ? SelectionMap.Normalize(selectionCommand.SelectionAfter, after)
            : SelectionMap.Normalize(beforeSelection, after);

        _undoStack.Push(new HistoryEntry(
            command,
            before,
            after,
            beforeSelection,
            afterSelection));
        _redoStack.Clear();
        Current = after;
        Selection = afterSelection;

        return Current;
    }

    public Document Undo()
    {
        if (!CanUndo)
        {
            return Current;
        }

        var entry = _undoStack.Pop();
        Current = entry.Before;
        Selection = entry.BeforeSelection;
        _redoStack.Push(entry);

        return Current;
    }

    public Document Redo()
    {
        if (!CanRedo)
        {
            return Current;
        }

        var entry = _redoStack.Pop();
        Current = entry.After;
        Selection = entry.AfterSelection;
        _undoStack.Push(entry);

        return Current;
    }

    public void SetSelection(Selection selection) =>
        Selection = SelectionMap.Normalize(selection, Current);

    private sealed record HistoryEntry(
        IEditCommand Command,
        Document Before,
        Document After,
        Selection BeforeSelection,
        Selection AfterSelection);
}
