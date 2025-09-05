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

    // Event raised when the undo/redo history changes (so UI can update)
    public event EventHandler? HistoryChanged;

    public void BatchActions(Action action)
    {
        var reentered = _batchingActions;

        if (!reentered)
        {
            _batchingActions = true;

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

            foreach (var action in actions)
            {
                action();
            }

            _suspendAddActions = false;
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

            foreach (var action in actions)
            {
                action();
            }

            _suspendAddActions = false;
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

    private void EnforceCapacity()
    {
        if (_redoActions.Count > MaximumHistorySize)
        {
            var first = _redoActions.First!;
            _redoActions.RemoveFirst();
            _redoActions.RemoveFirst();
            _redoActions.AddFirst(first);
        }

        if (_undoActions.Count > MaximumHistorySize)
        {
            var first = _undoActions.First!;
            _undoActions.RemoveFirst();
            _undoActions.RemoveFirst();
            _undoActions.AddFirst(first);
        }
    }

    private void OnHistoryChanged()
    {
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
