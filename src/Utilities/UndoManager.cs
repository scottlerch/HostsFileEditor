// <copyright file="UndoManager.cs" company="N/A">
// Copyright 2025 Scott M. Lerch
// 
// This file is part of HostsFileEditor.
// 
// HostsFileEditor is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free 
// Software Foundation, either version 2 of the License, or (at your option)
// any later version.
// 
// HostsFileEditor is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
// 
// You should have received a copy of the GNU General Public   License along
// with HostsFileEditor. If not, see http://www.gnu.org/licenses/.
// </copyright>

namespace HostsFileEditor.Utilities;

/// <summary>
/// Undo/redo manager used to keep track of actions performed
/// on an object.
/// </summary>
/// <remarks>
/// This code needs to be cleaned up and refactored since 
/// it's a bit confusing.
/// </remarks>
public class UndoManager
{
    /// <summary>
    /// Maximum history size.
    /// </summary>
    private const int MaximumHistorySize = 1000;

    /// <summary>
    /// Singleton instance.
    /// </summary>
    private static readonly Lazy<UndoManager> instance = 
        new(() => new UndoManager());

    /// <summary>
    /// Undo actions history.
    /// </summary>
    private readonly LinkedList<LinkedList<Action>> undoActions = 
        new();

    /// <summary>
    /// Current position in undo history.
    /// </summary>
    private LinkedListNode<LinkedList<Action>> undoActionsPosition;

    /// <summary>
    /// Redo actions history.
    /// </summary>
    private readonly LinkedList<LinkedList<Action>> redoActions = 
        new();

    /// <summary>
    /// Current position in redo history.
    /// </summary>
    private LinkedListNode<LinkedList<Action>> redoActionsPosition;

    /// <summary>
    /// Undo in progress.
    /// </summary>
    private bool undoInProgress;

    /// <summary>
    /// Redo in progress.
    /// </summary>
    private bool redoInProgress;

    /// <summary>
    /// Determines if currently batching actions into a single undo/redo command.
    /// </summary>
    private bool batchingActions;

    /// <summary>
    /// Suspend adding actions to undo/redo lists.
    /// </summary>
    private bool suspendAddActions;

    /// <summary>
    /// Prevents a default instance of the <see cref="UndoManager"/> class from being created.
    /// </summary>
    private UndoManager()
    {
        ClearHistory();
    }

    /// <summary>
    /// Gets the instance.
    /// </summary>
    public static UndoManager Instance 
    { 
        get { return instance.Value; } 
    }

    /// <summary>
    /// Batches the actions into a single undo/redo command.
    /// </summary>
    /// <param name="action">The action.</param>
    public void BatchActions(Action action)
    {
        bool reentered = batchingActions;

        if (!reentered)
        {
            batchingActions = true;

            undoActions.AddAfter(undoActionsPosition, new LinkedList<Action>());
            undoActionsPosition = undoActionsPosition.Next;

            redoActions.AddAfter(redoActionsPosition, new LinkedList<Action>());
            redoActionsPosition = redoActionsPosition.Next;
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

                // If no actions added to batch just removed empty data structures
                if (undoActionsPosition.Value.Count == 0)
                {
                    var previous = undoActionsPosition.Previous;

                    undoActions.Remove(undoActionsPosition);
                    undoActionsPosition = previous;
                }

                if (redoActionsPosition.Value.Count == 0)
                {
                    var previous = redoActionsPosition.Previous;

                    redoActions.Remove(redoActionsPosition);
                    redoActionsPosition = previous;
                }

                EnforceCapacity();
            }
        }
    }

    /// <summary>
    /// Performs the action.
    /// </summary>
    /// <param name="undoAction">The undo action.</param>
    /// <param name="redoAction">The redo action.</param>
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

                    undoActionsPosition = undoActionsPosition.Next;
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

                    redoActionsPosition = redoActionsPosition.Next;
                }
            }

            EnforceCapacity();
        }
    }

    /// <summary>
    /// Undo the last change.
    /// </summary>
    public void Undo()
    {
        if (undoActionsPosition != undoActions.First)
        {
            var actions = undoActionsPosition.Value;

            undoActionsPosition = undoActionsPosition.Previous;
            redoActionsPosition = redoActionsPosition.Previous;

            suspendAddActions = true;

            foreach (var action in actions)
            {
                action();
            }

            suspendAddActions = false;
        }
    }

    /// <summary>
    /// Redo the last change.
    /// </summary>
    public void Redo()
    {
        if (redoActionsPosition != redoActions.Last)
        {
            undoActionsPosition = undoActionsPosition.Next;
            redoActionsPosition = redoActionsPosition.Next;

            var actions = redoActionsPosition.Value;

            suspendAddActions = true;

            foreach (var action in actions)
            {
                action();
            }

            suspendAddActions = false;
        }
    }

    /// <summary>
    /// Clears the undo/redo history.
    /// </summary>
    public void ClearHistory()
    {
        // Have one dummy entry in the beginning of the lists so
        // there is always a node to insert after

        undoActions.Clear();
        undoActions.AddLast(new LinkedList<Action>());
        undoActionsPosition = undoActions.First;

        redoActions.Clear();
        redoActions.AddLast(new LinkedList<Action>());
        redoActionsPosition = redoActions.First;
    }

    /// <summary>
    /// Suspends the undo functionality.
    /// </summary>
    /// <param name="action">The action.</param>
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

    /// <summary>
    /// Suspends the redo functionality.
    /// </summary>
    /// <param name="action">The action.</param>
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

    /// <summary>
    /// Suspends the undo/redo functionality.
    /// </summary>
    /// <param name="action">The action.</param>
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

    /// <summary>
    /// Enforces the capacity of the undo history.
    /// </summary>
    private void EnforceCapacity()
    {
        if (redoActions.Count > MaximumHistorySize)
        {
            var first = redoActions.First;
            redoActions.RemoveFirst();
            redoActions.RemoveFirst();
            redoActions.AddFirst(first);
        }

        if (undoActions.Count > MaximumHistorySize)
        {
            var first = undoActions.First;
            undoActions.RemoveFirst();
            undoActions.RemoveFirst();
            undoActions.AddFirst(first);
        }
    }
}
