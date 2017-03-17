// <copyright file="HostsEntryList.cs" company="N/A">
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

namespace HostsFileEditor
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;

    using HostsFileEditor.Extensions;
    using HostsFileEditor.Properties;
    using HostsFileEditor.Utilities;

    /// <summary>
    /// This class represents one or more host entries.
    /// </summary>
    internal class HostsEntryList : BindingList<HostsEntry>
    {
        #region Fields and Constants

        /// <summary>
        /// Default hosts file lines.
        /// </summary>
        public static readonly string[] DefaultLines = Resources.hosts.Split(
            new[] { Environment.NewLine }, 
            StringSplitOptions.None);

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsEntryList"/> class.
        /// </summary>
        /// <param name="entryLines">
        /// The entry lines.
        /// </param>
        /// <param name="filterDefault">
        /// if set to <c>true</c> filter default text.
        /// </param>
        public HostsEntryList(IEnumerable<string> entryLines, bool filterDefault)
            : this()
        {
            this.AddLines(entryLines, filterDefault);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsEntryList"/> class.
        /// </summary>
        public HostsEntryList()
        {
            this.AllowEdit = true;
            this.AllowNew = true;
            this.AllowRemove = true;
            this.RaiseListChangedEvents = true;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the error text.
        /// </summary>
        public string Error
        {
            get { return this.Any(entry => !entry.Valid) ? Resources.InvalidHostEntries : string.Empty;  }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// The add lines.
        /// </summary>
        /// <param name="lines">The lines.</param>
        /// <param name="removeDefault">
        /// if set to <c>true</c> remove default entries.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public void AddLines(IEnumerable<string> lines, bool removeDefault = true)
        {
            lines.ThrowIfNull("lines");

            UndoManager.Instance.SuspendUndoRedo(() =>
            {
                int index = 0;
                foreach (string line in lines)
                {
                    bool isDefaultLine =
                        index < DefaultLines.Length &&
                        line.Trim() == DefaultLines[index++].Trim();

                    if (!removeDefault || !isDefaultLine)
                    {
                        this.Add(new HostsEntry(line));
                    }
                }
            });
        }

        /// <summary>
        /// Move items before specified item.
        /// </summary>
        /// <param name="entries">The entries to move.</param>
        /// <param name="beforeEntry">Host entry to move entries before.</param>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public void MoveBefore(IEnumerable<HostsEntry> entries, HostsEntry beforeEntry)
        {
            entries.ThrowIfNull("entries");
            beforeEntry.ThrowIfNull("beforeEntry");

            this.BatchUpdate(() =>
            {
                var copy = entries.ToList();
                int insertIndex = this.IndexOf(beforeEntry) - 1;

                if (insertIndex >= 0)
                {
                    UndoManager.Instance.BatchActions(() =>
                    {
                        this.Remove(copy);

                        if (insertIndex > this.Count)
                        {
                            insertIndex = this.Count;
                        }
                        else if (insertIndex < 0)
                        {
                            insertIndex = 0;
                        }

                        foreach (HostsEntry entry in copy)
                        {
                            this.Insert(insertIndex++, entry);
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Moves items after specified item.
        /// </summary>
        /// <param name="entries">The entries to move.</param>
        /// <param name="afterEntry">Host entry to move entries before.</param>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public void MoveAfter(IEnumerable<HostsEntry> entries, HostsEntry afterEntry)
        {
            entries.ThrowIfNull("entries");
            afterEntry.ThrowIfNull("afterEntry");

            this.BatchUpdate(() =>
            {
                var copy = entries.ToList();
                int insertIndex = this.IndexOf(afterEntry) + 1;

                if (insertIndex < this.Count)
                {
                    UndoManager.Instance.BatchActions(() =>
                    {
                        this.Remove(copy);

                        if (insertIndex > this.Count)
                        {
                            insertIndex = this.Count;
                        }
                        else if (insertIndex < 0)
                        {
                            insertIndex = 0;
                        }

                        foreach (HostsEntry entry in copy)
                        {
                            this.Insert(insertIndex++, entry);
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Inserts new host entry before specified host entry.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public void InsertBefore(HostsEntry entry, HostsEntry newEntry = null)
        {
            entry.ThrowIfNull("entry");

            int insertIndex = this.IndexOf(entry);
            this.Insert(insertIndex, newEntry ?? new HostsEntry());
        }

        /// <summary>
        /// Inserts new host entry after specified host entry.
        /// </summary>
        /// <param name="entry">The entry.</param>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public void InsertAfter(HostsEntry entry, HostsEntry newEntry = null)
        {
            entry.ThrowIfNull("entry");

            int insertIndex = this.IndexOf(entry) + 1;
            this.Insert(insertIndex, newEntry ?? new HostsEntry());
        }

        /// <summary>
        /// Inserts entries after entry.
        /// </summary>
        /// <param name="entry">The entry to insert before.</param>
        /// <param name="entries">The entries to insert.</param>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public void Insert(HostsEntry entry, IEnumerable<HostsEntry> entries)
        {
            entry.ThrowIfNull("entry");
            entry.ThrowIfNull("entries");

            int insertIndex = this.IndexOf(entry);

            UndoManager.Instance.BatchActions(() =>
            {
                foreach (var newEntry in entries.ToList())
                {
                    this.Insert(insertIndex++, newEntry);
                }
            });
        }

        /// <summary>
        /// The remove.
        /// </summary>
        /// <param name="entries">
        /// The event arguments.tries.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Argument cannot be null.
        /// </exception>
        public void Remove(IEnumerable<HostsEntry> entries)
        {
            entries.ThrowIfNull("entries");

            this.BatchUpdate(() =>
            {
                UndoManager.Instance.BatchActions(() =>
                {
                    foreach (HostsEntry entry in entries.ToList())
                    {
                        this.Remove(entry);
                    }
                });
            });
        }

        /// <summary>
        /// Adds new host entry.
        /// </summary>
        public void Add()
        {
            this.Add(new HostsEntry());
        }

        /// <summary>
        /// Checks the specified hosts entries.
        /// </summary>
        /// <param name="entries">The entries.</param>
        /// <param name="?">if set to <c>true</c> check items.</param>
        public void SetEnabled(IEnumerable<HostsEntry> entries, bool isEnabled)
        {
            entries.ThrowIfNull("entries");

            this.BatchUpdate(() =>
            {
                UndoManager.Instance.BatchActions(() =>
                {
                    foreach (HostsEntry entry in entries)
                    {
                        entry.Enabled = isEnabled;
                    }
                });
            });
        }

        #endregion

        #region Methods

        /// <summary>
        /// Create new object for list.
        /// </summary>
        /// <returns>New object for list</returns>
        protected override object AddNewCore()
        {
            return new HostsEntry(string.Empty);
        }

        /// <summary>
        /// Inserts the item.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        protected override void InsertItem(int index, HostsEntry item)
        {
            UndoManager.Instance.AddActions(
                undoAction: () => this.Remove(item), 
                redoAction: () => this.Insert(index, item));

            base.InsertItem(index, item);
        }

        /// <summary>
        /// Removes the item.
        /// </summary>
        /// <param name="index">The index.</param>
        protected override void RemoveItem(int index)
        {
            var item = this[index];

            UndoManager.Instance.AddActions(
                undoAction: () => this.Insert(index, item),
                redoAction: () => this.Remove(item));

            base.RemoveItem(index);
        }

        #endregion
    }
}