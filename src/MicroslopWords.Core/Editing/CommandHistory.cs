using MicroslopWords.Core.Documents;

namespace MicroslopWords.Core.Editing;

public sealed class CommandHistory
{
    public static readonly TimeSpan DefaultTypingCoalesceWindow =
        TimeSpan.FromSeconds(1);

    private readonly Stack<HistoryEntry> _undoStack = new();
    private readonly Stack<HistoryEntry> _redoStack = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _typingCoalesceWindow;
    private bool _coalescingBroken = true;
    private DateTimeOffset _lastTypingTime;

    public CommandHistory(
        Document initial,
        Selection? selection = null,
        TimeProvider? timeProvider = null,
        TimeSpan? typingCoalesceWindow = null)
    {
        ArgumentNullException.ThrowIfNull(initial);

        if (typingCoalesceWindow is { } window && window < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(typingCoalesceWindow),
                "The coalescing window cannot be negative.");
        }

        Current = initial;
        Selection = SelectionMap.Normalize(
            selection ?? Selection.StartOf(initial),
            initial);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _typingCoalesceWindow = typingCoalesceWindow ?? DefaultTypingCoalesceWindow;
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
        var now = _timeProvider.GetUtcNow();
        var coalesced = TryCoalesceTyping(
            command,
            after,
            afterSelection,
            now);

        if (!coalesced)
        {
            _undoStack.Push(new HistoryEntry(
                command,
                before,
                after,
                beforeSelection,
                afterSelection));
        }

        _redoStack.Clear();
        Current = after;
        Selection = afterSelection;
        _lastTypingTime = now;
        _coalescingBroken = false;

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
        _coalescingBroken = true;

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
        _coalescingBroken = true;

        return Current;
    }

    public void SetSelection(Selection selection)
    {
        var normalized = SelectionMap.Normalize(selection, Current);

        if (normalized != Selection)
        {
            _coalescingBroken = true;
        }

        Selection = normalized;
    }

    public void BreakCoalescing() => _coalescingBroken = true;

    private bool TryCoalesceTyping(
        IEditCommand command,
        Document after,
        Selection afterSelection,
        DateTimeOffset now)
    {
        if (_coalescingBroken
            || _undoStack.Count == 0
            || command is not InsertTextAtSelectionCommand currentTyping
            || !currentTyping.CanCoalesce)
        {
            return false;
        }

        var top = _undoStack.Peek();

        if (top.Command is not InsertTextAtSelectionCommand previousTyping
            || !previousTyping.CanCoalesce
            || !PositionsEqual(
                top.AfterSelection.Focus,
                currentTyping.SelectionBefore.Focus)
            || now - _lastTypingTime > _typingCoalesceWindow)
        {
            return false;
        }

        _undoStack.Pop();
        _undoStack.Push(top with
        {
            After = after,
            AfterSelection = afterSelection,
        });

        return true;
    }

    private static bool PositionsEqual(TextPosition left, TextPosition right) =>
        left.ParagraphId == right.ParagraphId
        && left.RunId == right.RunId
        && left.Offset == right.Offset;

    private sealed record HistoryEntry(
        IEditCommand Command,
        Document Before,
        Document After,
        Selection BeforeSelection,
        Selection AfterSelection);
}
