namespace HostsFileEditor.Utilities;

public class UndoManager
{
    private const int MaximumHistorySize = 1000;

    private static readonly Lazy<UndoManager> instance = new(() => new UndoManager());

    private readonly LinkedList<LinkedList<Action>> undoActions = new();

    private LinkedListNode<LinkedList<Action>> undoActionsPosition;

    private readonly LinkedList<LinkedList<Action>> redoActions = new();

    private LinkedListNode<LinkedList<Action>> redoActionsPosition;

    private bool undoInProgress;

    private bool redoInProgress;

    private bool batchingActions;

    private bool suspendAddActions;

    private UndoManager()
    {
        undoActionsPosition = undoActions.AddLast(new LinkedList<Action>());
        redoActionsPosition = redoActions.AddLast(new LinkedList<Action>());
    }

    public static UndoManager Instance => instance.Value;

    public void BatchActions(Action action)
    {
        var reentered = batchingActions;

        if (!reentered)
        {
            batchingActions = true;

            undoActions.AddAfter(undoActionsPosition, new LinkedList<Action>());
            undoActionsPosition = undoActionsPosition.Next!;

            redoActions.AddAfter(redoActionsPosition, new LinkedList<Action>());
            redoActionsPosition = redoActionsPosition.Next!;
        }

        try
        {
            action();
        }
        finally
        {
            if (!reentered)
            {
                batchingActions = false;

                if (undoActionsPosition.Value.Count == 0)
                {
                    LinkedListNode<LinkedList<Action>> previous = undoActionsPosition.Previous!;

                    undoActions.Remove(undoActionsPosition);
                    undoActionsPosition = previous;
                }

                if (redoActionsPosition.Value.Count == 0)
                {
                    LinkedListNode<LinkedList<Action>> previous = redoActionsPosition.Previous!;

                    redoActions.Remove(redoActionsPosition);
                    redoActionsPosition = previous;
                }

                EnforceCapacity();
            }
        }
    }

    public void AddActions(Action undoAction, Action redoAction)
    {
        if (!suspendAddActions)
        {
            if (!undoInProgress)
            {
                if (batchingActions)
                {
                    undoActionsPosition.Value.AddFirst(undoAction);
                }
                else
                {
                    undoActions.AddAfter(
                        undoActionsPosition,
                        new LinkedList<Action>([undoAction]));

                    undoActionsPosition = undoActionsPosition.Next!;
                }
            }

            if (!redoInProgress)
            {
                if (batchingActions)
                {
                    redoActionsPosition.Value.AddLast(redoAction);
                }
                else
                {
                    redoActions.AddAfter(
                        redoActionsPosition,
                        new LinkedList<Action>([redoAction]));

                    redoActionsPosition = redoActionsPosition.Next!;
                }
            }

            EnforceCapacity();
        }
    }

    public void Undo()
    {
        if (undoActionsPosition != undoActions.First)
        {
            LinkedList<Action> actions = undoActionsPosition.Value;

            undoActionsPosition = undoActionsPosition.Previous!;
            redoActionsPosition = redoActionsPosition.Previous!;

            suspendAddActions = true;

            foreach (Action action in actions)
            {
                action();
            }

            suspendAddActions = false;
        }
    }

    public void Redo()
    {
        if (redoActionsPosition != redoActions.Last)
        {
            undoActionsPosition = undoActionsPosition.Next!;
            redoActionsPosition = redoActionsPosition.Next!;

            LinkedList<Action> actions = redoActionsPosition.Value;

            suspendAddActions = true;

            foreach (Action action in actions)
            {
                action();
            }

            suspendAddActions = false;
        }
    }

    public void ClearHistory()
    {
        undoActions.Clear();
        undoActionsPosition = undoActions.AddLast(new LinkedList<Action>());

        redoActions.Clear();
        redoActionsPosition = redoActions.AddLast(new LinkedList<Action>());
    }

    public void SuspendUndo(Action action)
    {
        undoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            undoInProgress = false;
        }
    }

    public void SuspendRedo(Action action)
    {
        redoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            redoInProgress = false;
        }
    }

    public void SuspendUndoRedo(Action action)
    {
        undoInProgress = true;
        redoInProgress = true;

        try
        {
            action();
        }
        finally
        {
            undoInProgress = false;
            redoInProgress = false;
        }
    }

    private void EnforceCapacity()
    {
        if (redoActions.Count > MaximumHistorySize)
        {
            LinkedListNode<LinkedList<Action>> first = redoActions.First!;
            redoActions.RemoveFirst();
            redoActions.RemoveFirst();
            redoActions.AddFirst(first);
        }

        if (undoActions.Count > MaximumHistorySize)
        {
            LinkedListNode<LinkedList<Action>> first = undoActions.First!;
            undoActions.RemoveFirst();
            undoActions.RemoveFirst();
            undoActions.AddFirst(first);
        }
    }
}
