using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public sealed class CommandHistory
{
    private readonly Stack<HistoryEntry> _undoStack = new();
    private readonly Stack<HistoryEntry> _redoStack = new();

    public CommandHistory(Document initial)
    {
        ArgumentNullException.ThrowIfNull(initial);
        Current = initial;
    }

    public Document Current { get; private set; }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public Document Execute(IEditCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var after = command.Apply(Current);
        _undoStack.Push(new HistoryEntry(command, Current, after));
        _redoStack.Clear();
        Current = after;
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
        _undoStack.Push(entry);
        return Current;
    }

    private sealed record HistoryEntry(IEditCommand Command, Document Before, Document After);
}
