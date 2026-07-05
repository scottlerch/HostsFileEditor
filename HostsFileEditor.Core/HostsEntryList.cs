using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace HostsFileEditor;

public class HostsEntryList : BindingList<HostsEntry>
{
    public static readonly string[] DefaultLines = Resources.hosts.Split(
        [Environment.NewLine],
        StringSplitOptions.None);

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList only used for basic add/remove/change notifications; PropertyDescriptor reflective paths not used.")]
    public HostsEntryList(IEnumerable<string> entryLines, bool filterDefault)
        : this()
    {
        AddLines(entryLines, filterDefault);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList only used for basic add/remove/change notifications; PropertyDescriptor reflective paths not used.")]
    public HostsEntryList()
    {
        AllowEdit = true;
        AllowNew = true;
        AllowRemove = true;
        RaiseListChangedEvents = true;
    }

    public string Error => this.Any(entry => !entry.Valid) ? Resources.InvalidHostEntries : string.Empty;

    public void AddLines(IEnumerable<string> lines, bool removeDefault = true)
    {
        ArgumentNullException.ThrowIfNull(lines);

        UndoManager.Instance.SuspendUndoRedo(() =>
        {
            var index = 0;
            foreach (var line in lines)
            {
                var isDefaultLine =
                    index < DefaultLines.Length &&
                    line.Trim() == DefaultLines[index++].Trim();

                if (!removeDefault || !isDefaultLine)
                {
                    Add(new HostsEntry(line));
                }
            }
        });
    }

    public void MoveBefore(IEnumerable<HostsEntry> entries, HostsEntry beforeEntry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(beforeEntry);

        this.BatchUpdate(() =>
        {
            var moving = entries.ToList();
            if (moving.Count == 0)
            {
                return;
            }

            // Capture original indices BEFORE any removals
            var originalIndices = moving.ToDictionary(e => e, e => IndexOf(e));
            var beforeIndex = IndexOf(beforeEntry);
            if (beforeIndex < 0)
            {
                return; // target not found
            }

            // Order moving entries by their original appearance in the list
            moving.Sort((x, y) => originalIndices[x].CompareTo(originalIndices[y]));

            // Compute insertion index after removals: shift left by how many moving items were before the target
            var removedBefore = moving.Count(e => originalIndices[e] < beforeIndex);
            var insertIndex = beforeIndex - removedBefore;
            if (insertIndex < 0)
            {
                insertIndex = 0;
            }

            UndoManager.Instance.BatchActions(() =>
            {
                // Remove using simple removal (one-by-one) to minimize re-ordering side effects
                foreach (var e in moving)
                {
                    // If already removed (duplicate in list not expected) skip
                    if (Contains(e))
                    {
                        base.Remove(e);
                    }
                }

                if (insertIndex > Count)
                {
                    insertIndex = Count;
                }

                foreach (var entry in moving)
                {
                    Insert(insertIndex++, entry);
                }
            });
        });
    }

    public void MoveAfter(IEnumerable<HostsEntry> entries, HostsEntry afterEntry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(afterEntry);

        this.BatchUpdate(() =>
        {
            var moving = entries.ToList();
            if (moving.Count == 0)
            {
                return;
            }

            var originalIndices = moving.ToDictionary(e => e, e => IndexOf(e));
            var afterIndex = IndexOf(afterEntry);
            if (afterIndex < 0)
            {
                return; // target not found
            }

            moving.Sort((x, y) => originalIndices[x].CompareTo(originalIndices[y]));

            var removedBefore = moving.Count(e => originalIndices[e] < afterIndex);
            var updatedAfterIndex = afterIndex - removedBefore; // index after removals
            var insertIndex = updatedAfterIndex + 1; // after the target

            UndoManager.Instance.BatchActions(() =>
            {
                foreach (var e in moving)
                {
                    if (Contains(e))
                    {
                        base.Remove(e);
                    }
                }

                if (insertIndex > Count)
                {
                    insertIndex = Count;
                }

                if (insertIndex < 0)
                {
                    insertIndex = 0;
                }

                foreach (var entry in moving)
                {
                    Insert(insertIndex++, entry);
                }
            });
        });
    }

    public void InsertBefore(HostsEntry entry, HostsEntry? newEntry = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // A UI "new row" placeholder (e.g. the grid's uncommitted add-row) isn't in the list
        // yet, so IndexOf returns -1; fall back to appending rather than Insert(-1, ...) throwing.
        var index = IndexOf(entry);
        Insert(index < 0 ? Count : index, newEntry ?? new HostsEntry());
    }

    public void InsertAfter(HostsEntry entry, HostsEntry? newEntry = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var index = IndexOf(entry);
        Insert(index < 0 ? Count : index + 1, newEntry ?? new HostsEntry());
    }

    public void Insert(HostsEntry entry, IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entries);

        var insertIndex = IndexOf(entry);
        if (insertIndex < 0)
        {
            insertIndex = Count;
        }

        UndoManager.Instance.BatchActions(() =>
        {
            foreach (var newEntry in entries.ToList())
            {
                Insert(insertIndex++, newEntry);
            }
        });
    }

    public void Remove(IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // Removing one-by-one is O(n²) for a large selection: each Remove(entry) is an O(n)
        // IndexOf plus an O(n) backing-store shift, and every removed row pushes its own undo
        // closure. Selecting-all then Remove/Cut on a huge file wedged the app for minutes.
        // Instead compute the survivors in one pass and replace the whole list in O(n), backed by
        // a single combined undo/redo action.
        // Own HashSet with the default (reference) comparer so membership can't be skewed by a
        // caller-supplied set built with a different comparer.
        var removeSet = new HashSet<HostsEntry>(entries);
        if (removeSet.Count == 0)
        {
            return;
        }

        // Single pass builds both the pre-removal snapshot (for undo) and the survivors (for the
        // removal itself), so Remove walks the list once instead of twice on the large-file path.
        var original = new List<HostsEntry>(Count);
        var survivors = new List<HostsEntry>(Count);
        foreach (var entry in this)
        {
            original.Add(entry);
            if (!removeSet.Contains(entry))
            {
                survivors.Add(entry);
            }
        }

        // No listed entry was actually present — nothing to do (and no spurious undo entry / event).
        if (survivors.Count == original.Count)
        {
            return;
        }

        // The undo action replaces the ENTIRE list, so this must not be nested inside an outer
        // UndoManager.BatchActions alongside other partial list mutations — on undo, ReplaceAll
        // would overwrite their effects. All callers invoke Remove as a standalone operation.
        if (!UndoManager.Instance.IsCapturingSuspended)
        {
            UndoManager.Instance.AddActions(
                undoAction: () => ReplaceAll(original),
                redoAction: () => ReplaceAll(survivors));
        }

        ReplaceAll(survivors);
    }

    // Replaces the entire list content in O(n): ClearItems unhooks every row's change tracking,
    // then the rows are re-added (re-hooking survivors). Wrapped in BatchUpdate so bound views get
    // exactly one ListChanged(Reset), and with undo capture suspended because callers register
    // their own combined undo/redo action.
    private void ReplaceAll(List<HostsEntry> target) =>
        this.BatchUpdate(() =>
            UndoManager.Instance.SuspendUndoRedo(() =>
            {
                ClearItems();
                foreach (var entry in target)
                {
                    base.InsertItem(Count, entry);
                }
            }));

    public void Add() => Add(new HostsEntry());

    public void SetEnabled(IEnumerable<HostsEntry> entries, bool isEnabled)
    {
        ArgumentNullException.ThrowIfNull(entries);

        this.BatchUpdate(() =>
        {
            UndoManager.Instance.BatchActions(() =>
            {
                foreach (var entry in entries)
                {
                    entry.Enabled = isEnabled;
                }
            });
        });
    }

    protected override object AddNewCore() => new HostsEntry(string.Empty);

    protected override void InsertItem(int index, HostsEntry item)
    {
        // Skip building the undo closures entirely when capturing is suspended (e.g. the bulk
        // load in AddLines) — otherwise every one of hundreds of thousands of inserts allocates
        // two throwaway delegates and fires HistoryChanged for nothing.
        if (!UndoManager.Instance.IsCapturingSuspended)
        {
            UndoManager.Instance.AddActions(
                undoAction: () => Remove(item),
                redoAction: () => Insert(index, item));
        }

        base.InsertItem(index, item);
    }

    protected override void RemoveItem(int index)
    {
        if (!UndoManager.Instance.IsCapturingSuspended)
        {
            var item = this[index];

            UndoManager.Instance.AddActions(
                undoAction: () => Insert(index, item),
                redoAction: () => Remove(item));
        }

        base.RemoveItem(index);
    }
}
