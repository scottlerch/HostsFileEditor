namespace HostsFileEditor.Utilities;

public class UndoManager
{
    private const int MaximumHistorySize = 1000;

    private static readonly Lazy<UndoManager> _instance = new(() => new UndoManager());

    private readonly LinkedList<LinkedList<Action>> _undoActions = new();

    private LinkedListNode<LinkedList<Action>> _undoActionsPosition;

    private readonly LinkedList<LinkedList<Action>> _redoActions = new();

    private LinkedListNode<LinkedList<Action>> _redoActionsPosition;

    private bool _undoInProgress;

    private bool _redoInProgress;

    private bool _batchingActions;

    private bool _suspendAddActions;

    private UndoManager()
    {
        _undoActionsPosition = _undoActions.AddLast(new LinkedList<Action>());
        _redoActionsPosition = _redoActions.AddLast(new LinkedList<Action>());
    }

    public static UndoManager Instance => _instance.Value;

    // New public properties to expose availability without reflection
    public bool CanUndo => _undoActionsPosition != _undoActions.First;
    public bool CanRedo => _redoActionsPosition != _redoActions.Last;

    // Opaque token identifying the current position in the undo history. It reference-compares
    // equal only when the history is back at the same position, so callers can detect whether
    // anything has changed since a captured token (e.g. unsaved edits since the last save). It
    // changes as actions are added/undone/redone and whenever the history is cleared.
    public object CurrentStateToken => _undoActionsPosition;

    // Event raised when the undo/redo history changes (so UI can update)
    public event EventHandler? HistoryChanged;

    public void BatchActions(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var reentered = _batchingActions;

        if (!reentered)
        {
            _batchingActions = true;

            // A new (batched) action invalidates the redo branch — the previously undone
            // groups that sit after the current position must be discarded, otherwise redo
            // would replay stale actions on top of the new one.
            RemoveNodesAfter(_undoActions, _undoActionsPosition);
            RemoveNodesAfter(_redoActions, _redoActionsPosition);

            _undoActions.AddAfter(_undoActionsPosition, new LinkedList<Action>());
            _undoActionsPosition = _undoActionsPosition.Next!;

            _redoActions.AddAfter(_redoActionsPosition, new LinkedList<Action>());
            _redoActionsPosition = _redoActionsPosition.Next!;
        }

        try
        {
            action();
        }
        finally
        {
            if (!reentered)
            {
                _batchingActions = false;

                if (_undoActionsPosition.Value.Count == 0)
                {
                    var previous = _undoActionsPosition.Previous!;

                    _undoActions.Remove(_undoActionsPosition);
                    _undoActionsPosition = previous;
                }

                if (_redoActionsPosition.Value.Count == 0)
                {
                    var previous = _redoActionsPosition.Previous!;

                    _redoActions.Remove(_redoActionsPosition);
                    _redoActionsPosition = previous;
                }

                EnforceCapacity();
                OnHistoryChanged();
            }
        }
    }

    public void AddActions(Action undoAction, Action redoAction)
    {
        if (!_suspendAddActions)
        {
            if (!_undoInProgress)
            {
                if (_batchingActions)
                {
                    _undoActionsPosition.Value.AddFirst(undoAction);
                }
                else
                {
                    // A new action invalidates the redo branch (the undone groups after
                    // the current position); discard them before appending so redo can't
                    // walk into stale history.
                    RemoveNodesAfter(_undoActions, _undoActionsPosition);

                    _undoActions.AddAfter(
                        _undoActionsPosition,
                        new LinkedList<Action>([undoAction]));

                    _undoActionsPosition = _undoActionsPosition.Next!;
                }
            }

            if (!_redoInProgress)
            {
                if (_batchingActions)
                {
                    _redoActionsPosition.Value.AddLast(redoAction);
                }
                else
                {
                    RemoveNodesAfter(_redoActions, _redoActionsPosition);

                    _redoActions.AddAfter(
                        _redoActionsPosition,
                        new LinkedList<Action>([redoAction]));

                    _redoActionsPosition = _redoActionsPosition.Next!;
                }
            }

            EnforceCapacity();
            OnHistoryChanged();
        }
    }

    public void Undo()
    {
        if (_undoActionsPosition != _undoActions.First)
        {
            var actions = _undoActionsPosition.Value;

            _undoActionsPosition = _undoActionsPosition.Previous!;
            _redoActionsPosition = _redoActionsPosition.Previous!;

            _suspendAddActions = true;

            try
            {
                foreach (var action in actions)
                {
                    action();
                }
            }
            finally
            {
                _suspendAddActions = false;
            }

            OnHistoryChanged();
        }
    }

    public void Redo()
    {
        if (_redoActionsPosition != _redoActions.Last)
        {
            _undoActionsPosition = _undoActionsPosition.Next!;
            _redoActionsPosition = _redoActionsPosition.Next!;

            var actions = _redoActionsPosition.Value;

            _suspendAddActions = true;

            try
            {
                foreach (var action in actions)
                {
                    action();
                }
            }
            finally
            {
                _suspendAddActions = false;
            }

            OnHistoryChanged();
        }
    }

    public void ClearHistory()
    {
        _undoActions.Clear();
        _undoActionsPosition = _undoActions.AddLast(new LinkedList<Action>());

        _redoActions.Clear();
        _redoActionsPosition = _redoActions.AddLast(new LinkedList<Action>());

        OnHistoryChanged();
    }

    public void SuspendUndo(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _undoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            _undoInProgress = false;
        }
    }

    public void SuspendRedo(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _redoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            _redoInProgress = false;
        }
    }

    public void SuspendUndoRedo(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        _undoInProgress = true;
        _redoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            _undoInProgress = false;
            _redoInProgress = false;
        }
    }

    // Discards every group after the given position (the redo branch). Called when a new
    // action is recorded so redo can no longer replay superseded history.
    private static void RemoveNodesAfter(
        LinkedList<LinkedList<Action>> actions,
        LinkedListNode<LinkedList<Action>> position)
    {
        while (position.Next is not null)
        {
            actions.Remove(position.Next);
        }
    }

    private void EnforceCapacity()
    {
        TrimOldest(_undoActions, ref _undoActionsPosition);
        TrimOldest(_redoActions, ref _redoActionsPosition);
    }

    // Evicts the oldest real action group (the node right after the sentinel First
    // node) while preserving the sentinel. If the current position points at the
    // node being evicted, it is advanced to the sentinel so navigation stays valid.
    private static void TrimOldest(
        LinkedList<LinkedList<Action>> actions,
        ref LinkedListNode<LinkedList<Action>> position)
    {
        if (actions.Count <= MaximumHistorySize)
        {
            return;
        }

        var oldest = actions.First!.Next;
        if (oldest is null)
        {
            return;
        }

        if (position == oldest)
        {
            position = actions.First!;
        }

        actions.Remove(oldest);
    }

    private void OnHistoryChanged() => HistoryChanged?.Invoke(this, EventArgs.Empty);
}
