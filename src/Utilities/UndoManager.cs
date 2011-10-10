// <copyright file="UndoManager.cs" company="N/A">
// Copyright 2011 Scott M. Lerch
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
    public class UndoManager
    {
        #region Constants and Fields

        /// <summary>
        /// Singleton instance.
        /// </summary>
        private static readonly Lazy<UndoManager> instance = new Lazy<UndoManager>(() => new UndoManager());

        /// <summary>
        /// Undo actions.
        /// </summary>
        private Stack<Action> undoActions = new Stack<Action>();

        /// <summary>
        /// Redo actions.
        /// </summary>
        private Queue<Action> redoActions = new Queue<Action>();

        /// <summary>
        /// Undo in progress.
        /// </summary>
        private bool undoInProgress;

        /// <summary>
        /// Redo in progress.
        /// </summary>
        private bool redoInProgress;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Prevents a default instance of the <see cref="UndoManager"/> class from being created.
        /// </summary>
        private UndoManager()
        {
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
        /// Performs the action.
        /// </summary>
        /// <param name="undoAction">The undo action.</param>
        /// <param name="redoAction">The redo action.</param>
        public void AddActions(Action undoAction, Action redoAction)
        {
            if (!this.undoInProgress)
            {
                this.undoActions.Push(() => this.SuspendUndo(undoAction));
            }

            if (!this.redoInProgress)
            {
                this.redoActions.Enqueue(() => this.SuspendRedo(redoAction));
            }
        }

        /// <summary>
        /// Undo the last change.
        /// </summary>
        public void Undo()
        {
            if (this.undoActions.Count > 0)
            {
                Action action = this.undoActions.Pop();
                action();
            }
        }

        /// <summary>
        /// Redo the last change.
        /// </summary>
        public void Redo()
        {
            if (this.redoActions.Count > 0)
            {
                Action action = this.redoActions.Dequeue();
                action();
            }
        }

        /// <summary>
        /// Clears the undo/redo history.
        /// </summary>
        public void ClearHistory()
        {
            this.undoActions.Clear();
            this.redoActions.Clear();
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

        #endregion
    }
}
