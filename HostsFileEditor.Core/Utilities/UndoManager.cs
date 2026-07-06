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

    // True when AddActions would capture nothing anyway, so hot paths (list InsertItem/RemoveItem
    // during a bulk load) can skip building throwaway undo closures and firing HistoryChanged for
    // every one of hundreds of thousands of items. Two cases:
    //   - _suspendAddActions: explicit suspension AND undo/redo replay (Undo()/Redo() set it).
    //   - _undoInProgress && _redoInProgress: both are set only by SuspendUndoRedo (bulk load /
    //     ReplaceAll). A single-sided SuspendUndo/SuspendRedo would NOT read as suspended here — no
    //     caller mutates the list under single-sided suspension, so that gap is not exercised.
    public bool IsCapturingSuspended => _suspendAddActions || (_undoInProgress && _redoInProgress);

    // Surrogate identity for the sentinel (empty-history) position. The sentinel NODE survives both
    // ClearHistory (a new node is allocated, but callers hold tokens, not nodes) and capacity
    // eviction — yet after TrimOldest discards the oldest real group, "back at the sentinel" no
    // longer means "back at the state the sentinel used to represent" (the evicted ops can never be
    // undone). Replacing this object on eviction/clear makes stale sentinel tokens compare unequal,
    // so HostsFile.IsModified can't report clean after undoing to a truncated history's floor.
    private object _sentinelStateToken = new();

    // Opaque token identifying the current position in the undo history. It reference-compares
    // equal only when the history is back at the same position AND that position still represents
    // the same state, so callers can detect whether anything has changed since a captured token
    // (e.g. unsaved edits since the last save). It changes as actions are added/undone/redone,
    // whenever the history is cleared, and whenever capacity eviction rebases the sentinel.
    public object CurrentStateToken =>
        _undoActionsPosition == _undoActions.First ? _sentinelStateToken : _undoActionsPosition;

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
        // A fully suspended capture must be a TRUE no-op. Without this early-out, a caller that
        // skipped its own IsCapturingSuspended guard would — under SuspendUndoRedo, where
        // _suspendAddActions is false but both replay flags are set — fall through both inner
        // blocks yet still run EnforceCapacity + OnHistoryChanged, firing a phantom HistoryChanged
        // (and a UI CanUndo/IsModified re-read) in the middle of a bulk replay. The call-site
        // guards in HostsEntryList remain purely as a hot-path optimization: they skip BUILDING the
        // throwaway undo/redo closures, which this callee-side check cannot do.
        if (IsCapturingSuspended)
        {
            return;
        }

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

        // A cleared history is a new state baseline; tokens captured before the clear must not
        // compare equal to the fresh sentinel position.
        _sentinelStateToken = new object();

        OnHistoryChanged();
    }

    // All three suspend helpers save and RESTORE the prior flag values (rather than resetting to
    // false) so they are safe to nest: an inner ReplaceAll's SuspendUndoRedo must not silently end
    // an outer suspension scope and let the rest of a bulk operation start capturing undo again.

    public void SuspendUndo(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var previous = _undoInProgress;
        _undoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            _undoInProgress = previous;
        }
    }

    public void SuspendRedo(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var previous = _redoInProgress;
        _redoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            _redoInProgress = previous;
        }
    }

    public void SuspendUndoRedo(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var previousUndo = _undoInProgress;
        var previousRedo = _redoInProgress;
        _undoInProgress = true;
        _redoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            _undoInProgress = previousUndo;
            _redoInProgress = previousRedo;
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
        if (TrimOldest(_undoActions, ref _undoActionsPosition))
        {
            // The evicted group can never be undone, so the sentinel no longer represents the state
            // tokens captured against it (e.g. HostsFile's clean-at-load marker). Rebase its identity
            // so those stale tokens read as "modified" instead of falsely clean after an undo-all.
            _sentinelStateToken = new object();
        }

        TrimOldest(_redoActions, ref _redoActionsPosition);
    }

    // Evicts the oldest real action group (the node right after the sentinel First
    // node) while preserving the sentinel. If the current position points at the
    // node being evicted, it is advanced to the sentinel so navigation stays valid.
    // Returns whether a group was evicted.
    private static bool TrimOldest(
        LinkedList<LinkedList<Action>> actions,
        ref LinkedListNode<LinkedList<Action>> position)
    {
        if (actions.Count <= MaximumHistorySize)
        {
            return false;
        }

        var oldest = actions.First!.Next;
        if (oldest is null)
        {
            return false;
        }

        if (position == oldest)
        {
            position = actions.First!;
        }

        actions.Remove(oldest);
        return true;
    }

    private void OnHistoryChanged() => HistoryChanged?.Invoke(this, EventArgs.Empty);
}
