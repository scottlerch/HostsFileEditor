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

namespace HostsFileEditor.Utilities
{
    using System;
    using System.Collections.Generic;

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
        #region Constants and Fields

        /// <summary>
        /// Maximum history size.
        /// </summary>
        private const int MaximumHistorySize = 1000;

        /// <summary>
        /// Singleton instance.
        /// </summary>
        private static readonly Lazy<UndoManager> instance = 
            new Lazy<UndoManager>(() => new UndoManager());

        /// <summary>
        /// Undo actions history.
        /// </summary>
        private LinkedList<LinkedList<Action>> undoActions = 
            new LinkedList<LinkedList<Action>>();

        /// <summary>
        /// Current position in undo history.
        /// </summary>
        private LinkedListNode<LinkedList<Action>> undoActionsPosition;

        /// <summary>
        /// Redo actions history.
        /// </summary>
        private LinkedList<LinkedList<Action>> redoActions = 
            new LinkedList<LinkedList<Action>>();

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

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Prevents a default instance of the <see cref="UndoManager"/> class from being created.
        /// </summary>
        private UndoManager()
        {
            this.ClearHistory();
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the instance.
        /// </summary>
        public static UndoManager Instance 
        { 
            get { return instance.Value; } 
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Batches the actions into a single undo/redo command.
        /// </summary>
        /// <param name="action">The action.</param>
        public void BatchActions(Action action)
        {
            bool reentered = this.batchingActions;

            if (!reentered)
            {
                this.batchingActions = true;

                this.undoActions.AddAfter(this.undoActionsPosition, new LinkedList<Action>());
                this.undoActionsPosition = this.undoActionsPosition.Next;

                this.redoActions.AddAfter(this.redoActionsPosition, new LinkedList<Action>());
                this.redoActionsPosition = this.redoActionsPosition.Next;
            }

            try
            {
                action();
            }
            finally
            {
                if (!reentered)
                {
                    this.batchingActions = false;

                    // If no actions added to batch just removed empty data structures
                    if (this.undoActionsPosition.Value.Count == 0)
                    {
                        var previous = this.undoActionsPosition.Previous;

                        this.undoActions.Remove(this.undoActionsPosition);
                        this.undoActionsPosition = previous;
                    }

                    if (this.redoActionsPosition.Value.Count == 0)
                    {
                        var previous = this.redoActionsPosition.Previous;

                        this.redoActions.Remove(this.redoActionsPosition);
                        this.redoActionsPosition = previous;
                    }

                    this.EnforceCapacity();
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
            if (!this.suspendAddActions)
            {
                if (!this.undoInProgress)
                {
                    if (this.batchingActions)
                    {
                        this.undoActionsPosition.Value.AddFirst(undoAction);
                    }
                    else
                    {
                        this.undoActions.AddAfter(
                            this.undoActionsPosition, 
                            new LinkedList<Action>(new Action[] { undoAction }));

                        this.undoActionsPosition = this.undoActionsPosition.Next;
                    }
                }

                if (!this.redoInProgress)
                {
                    if (this.batchingActions)
                    {
                        this.redoActionsPosition.Value.AddLast(redoAction);
                    }
                    else
                    {
                        this.redoActions.AddAfter(
                            this.redoActionsPosition, 
                            new LinkedList<Action>(new Action[] { redoAction }));

                        this.redoActionsPosition = this.redoActionsPosition.Next;
                    }
                }

                this.EnforceCapacity();
            }
        }

        /// <summary>
        /// Undo the last change.
        /// </summary>
        public void Undo()
        {
            if (this.undoActionsPosition != this.undoActions.First)
            {
                var actions = this.undoActionsPosition.Value;

                this.undoActionsPosition = this.undoActionsPosition.Previous;
                this.redoActionsPosition = this.redoActionsPosition.Previous;

                this.suspendAddActions = true;

                foreach (var action in actions)
                {
                    action();
                }

                this.suspendAddActions = false;
            }
        }

        /// <summary>
        /// Redo the last change.
        /// </summary>
        public void Redo()
        {
            if (this.redoActionsPosition != this.redoActions.Last)
            {
                this.undoActionsPosition = this.undoActionsPosition.Next;
                this.redoActionsPosition = this.redoActionsPosition.Next;

                var actions = this.redoActionsPosition.Value;

                this.suspendAddActions = true;

                foreach (var action in actions)
                {
                    action();
                }

                this.suspendAddActions = false;
            }
        }

        /// <summary>
        /// Clears the undo/redo history.
        /// </summary>
        public void ClearHistory()
        {
            // Have one dummy entry in the beginning of the lists so
            // there is always a node to insert after

            this.undoActions.Clear();
            this.undoActions.AddLast(new LinkedList<Action>());
            this.undoActionsPosition = this.undoActions.First;

            this.redoActions.Clear();
            this.redoActions.AddLast(new LinkedList<Action>());
            this.redoActionsPosition = this.redoActions.First;
        }

        /// <summary>
        /// Suspends the undo functionality.
        /// </summary>
        /// <param name="action">The action.</param>
        public void SuspendUndo(Action action)
        {
            this.undoInProgress = true;

            try
            {
                action();
            }
            finally
            {
                this.undoInProgress = false;
            }
        }

        /// <summary>
        /// Suspends the redo functionality.
        /// </summary>
        /// <param name="action">The action.</param>
        public void SuspendRedo(Action action)
        {
            this.redoInProgress = true;

            try
            {
                action();
            }
            finally
            {
                this.redoInProgress = false;
            }
        }

        /// <summary>
        /// Suspends the undo/redo functionality.
        /// </summary>
        /// <param name="action">The action.</param>
        public void SuspendUndoRedo(Action action)
        {
            this.undoInProgress = true;
            this.redoInProgress = true;

            try
            {
                action();
            }
            finally
            {
                this.undoInProgress = false;
                this.redoInProgress = false;
            }
        }

        /// <summary>
        /// Enforces the capacity of the undo history.
        /// </summary>
        private void EnforceCapacity()
        {
            if (this.redoActions.Count > MaximumHistorySize)
            {
                var first = this.redoActions.First;
                this.redoActions.RemoveFirst();
                this.redoActions.RemoveFirst();
                this.redoActions.AddFirst(first);
            }

            if (this.undoActions.Count > MaximumHistorySize)
            {
                var first = this.undoActions.First;
                this.undoActions.RemoveFirst();
                this.undoActions.RemoveFirst();
                this.undoActions.AddFirst(first);
            }
        }

        #endregion
    }
}
