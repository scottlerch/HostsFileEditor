// <copyright file="HostsEntryList.cs" company="N/A">
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

using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.ComponentModel;

namespace HostsFileEditor;

/// <summary>
/// This class represents one or more host entries.
/// </summary>
internal class HostsEntryList : BindingList<HostsEntry>
{
    /// <summary>
    /// Default hosts file lines.
    /// </summary>
    public static readonly string[] DefaultLines = Resources.hosts.Split(
        [Environment.NewLine],
        StringSplitOptions.None);

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
        AddLines(entryLines, filterDefault);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsEntryList"/> class.
    /// </summary>
    public HostsEntryList()
    {
        AllowEdit = true;
        AllowNew = true;
        AllowRemove = true;
        RaiseListChangedEvents = true;
    }

    /// <summary>
    /// Gets the error text.
    /// </summary>
    public string Error => this.Any(entry => !entry.Valid) ? Resources.InvalidHostEntries : string.Empty;

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
        ArgumentNullException.ThrowIfNull(lines);

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
                    Add(new HostsEntry(line));
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
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(beforeEntry);

        this.BatchUpdate(() =>
        {
            var copy = entries.ToList();
            int insertIndex = IndexOf(beforeEntry) - 1;

            if (insertIndex >= 0)
            {
                UndoManager.Instance.BatchActions(() =>
                {
                    Remove(copy);

                    if (insertIndex > Count)
                    {
                        insertIndex = Count;
                    }
                    else if (insertIndex < 0)
                    {
                        insertIndex = 0;
                    }

                    foreach (HostsEntry entry in copy)
                    {
                        Insert(insertIndex++, entry);
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
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(afterEntry);

        this.BatchUpdate(() =>
        {
            var copy = entries.ToList();
            int insertIndex = IndexOf(afterEntry) + 1;

            if (insertIndex < Count)
            {
                UndoManager.Instance.BatchActions(() =>
                {
                    Remove(copy);

                    if (insertIndex > Count)
                    {
                        insertIndex = Count;
                    }
                    else if (insertIndex < 0)
                    {
                        insertIndex = 0;
                    }

                    foreach (HostsEntry entry in copy)
                    {
                        Insert(insertIndex++, entry);
                    }
                });
            }
        });
    }

    /// <summary>
    /// Inserts new host entry before specified host entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="newEntry">The new entry to insert.</param>
    /// <exception cref="ArgumentNullException">
    /// Argument cannot be null.
    /// </exception>
    public void InsertBefore(HostsEntry entry, HostsEntry? newEntry = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        int insertIndex = IndexOf(entry);
        Insert(insertIndex, newEntry ?? new HostsEntry());
    }

    /// <summary>
    /// Inserts new host entry after specified host entry.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <param name="newEntry">The new entry to insert.</param>
    /// <exception cref="ArgumentNullException">
    /// Argument cannot be null.
    /// </exception>
    public void InsertAfter(HostsEntry entry, HostsEntry? newEntry = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        int insertIndex = IndexOf(entry) + 1;
        Insert(insertIndex, newEntry ?? new HostsEntry());
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
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entries);

        int insertIndex = IndexOf(entry);

        UndoManager.Instance.BatchActions(() =>
        {
            foreach (var newEntry in entries.ToList())
            {
                Insert(insertIndex++, newEntry);
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
        ArgumentNullException.ThrowIfNull(entries);

        this.BatchUpdate(() =>
        {
            UndoManager.Instance.BatchActions(() =>
            {
                foreach (HostsEntry entry in entries.ToList())
                {
                    Remove(entry);
                }
            });
        });
    }

    /// <summary>
    /// Adds new host entry.
    /// </summary>
    public void Add()
    {
        Add(new HostsEntry());
    }

    /// <summary>
    /// Checks the specified hosts entries.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <param name="?">if set to <c>true</c> check items.</param>
    public void SetEnabled(IEnumerable<HostsEntry> entries, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(entries);

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

    /// <inheritdoc />
    protected override object AddNewCore()
    {
        return new HostsEntry(string.Empty);
    }

    /// <inheritdoc />
    protected override void InsertItem(int index, HostsEntry item)
    {
        UndoManager.Instance.AddActions(
            undoAction: () => Remove(item), 
            redoAction: () => Insert(index, item));

        base.InsertItem(index, item);
    }

    /// <inheritdoc />
    protected override void RemoveItem(int index)
    {
        var item = this[index];

        UndoManager.Instance.AddActions(
            undoAction: () => Insert(index, item),
            redoAction: () => Remove(item));

        base.RemoveItem(index);
    }
}