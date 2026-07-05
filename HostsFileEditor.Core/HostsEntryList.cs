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
        MoveRelative(entries, beforeEntry, insertAfterAnchor: false);
    }

    public void MoveAfter(IEnumerable<HostsEntry> entries, HostsEntry afterEntry)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(afterEntry);
        MoveRelative(entries, afterEntry, insertAfterAnchor: true);
    }

    // Moves the given entries as a contiguous block to immediately before/after the anchor in one
    // O(n) rebuild with a single Reset. The previous per-row base.Remove + Insert loop was O(k*n)
    // (each an O(n) IndexOf + backing-store shift) and pushed 2 undo closures per row — the last
    // O(n^2) hang left in the app on a large multi-row move. The anchor is excluded from the moving
    // set so "move relative to a row inside the selection" stays a no-op (matches Move Up/Down, whose
    // callers anchor on the row just outside the selection). Moving rows keep their relative order.
    private void MoveRelative(IEnumerable<HostsEntry> entries, HostsEntry anchor, bool insertAfterAnchor)
    {
        var moveSet = new HashSet<HostsEntry>(entries);
        moveSet.Remove(anchor);
        if (moveSet.Count == 0)
        {
            return;
        }

        var original = this.ToList();
        if (!original.Contains(anchor) || !original.Any(moveSet.Contains))
        {
            return;
        }

        ReplaceAllUndoable(original, () =>
        {
            var moving = original.Where(moveSet.Contains).ToList();
            var updated = new List<HostsEntry>(original.Count);
            foreach (var entry in original)
            {
                if (moveSet.Contains(entry))
                {
                    continue;
                }

                if (insertAfterAnchor)
                {
                    updated.Add(entry);
                    if (ReferenceEquals(entry, anchor))
                    {
                        updated.AddRange(moving);
                    }
                }
                else
                {
                    if (ReferenceEquals(entry, anchor))
                    {
                        updated.AddRange(moving);
                    }

                    updated.Add(entry);
                }
            }

            return updated;
        });
    }

    public void InsertBefore(HostsEntry entry, HostsEntry? newEntry = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // A UI "new row" placeholder (e.g. the grid's uncommitted add-row) isn't in the list
        // yet, so IndexOf returns -1; fall back to appending rather than Insert(-1, ...) throwing.
        var index = IndexOf(entry);
        InsertAll(index < 0 ? Count : index, [newEntry ?? new HostsEntry()]);
    }

    public void InsertAfter(HostsEntry entry, HostsEntry? newEntry = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var index = IndexOf(entry);
        InsertAll(index < 0 ? Count : index + 1, [newEntry ?? new HostsEntry()]);
    }

    public void Insert(HostsEntry entry, IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entries);

        var index = IndexOf(entry);
        InsertAll(index < 0 ? Count : index, entries as IReadOnlyList<HostsEntry> ?? [.. entries]);
    }

    // Appends entries at the end. Used to paste into an empty list (e.g. after Cut-All), where there
    // is no anchor row to insert relative to.
    public void InsertRange(IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        InsertAll(Count, entries as IReadOnlyList<HostsEntry> ?? [.. entries]);
    }

    // Inserts a copy of each of the given entries immediately after it, in a single O(n) rebind with
    // one Reset and one undo action. Callers used to loop InsertAfter per row, but each insert is now
    // a full O(n) rebind (see InsertAll), so duplicating a large selection that way is O(n^2) — a
    // Duplicate-All on a 400K file would hang for many minutes. Reference identity distinguishes the
    // originals to copy from the copies being appended, so copies are never themselves duplicated.
    public void Duplicate(IReadOnlyCollection<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return;
        }

        var toDuplicate = new HashSet<HostsEntry>(entries);
        var original = this.ToList();

        // Create each copy exactly once: redo must restore the SAME copy instances (so a later undo
        // removes them and object identity stays stable), and rebuilding the interleaved order from
        // this map avoids retaining a second whole-list snapshot. Reference identity distinguishes the
        // originals to copy from the copies being appended, so copies are never themselves duplicated.
        var copies = new Dictionary<HostsEntry, HostsEntry>();
        foreach (var entry in original)
        {
            if (toDuplicate.Contains(entry))
            {
                copies[entry] = new HostsEntry(entry);
            }
        }

        if (copies.Count == 0)
        {
            return;
        }

        ReplaceAllUndoable(original, () =>
        {
            var updated = new List<HostsEntry>(original.Count + copies.Count);
            foreach (var entry in original)
            {
                updated.Add(entry);
                if (copies.TryGetValue(entry, out var copy))
                {
                    updated.Add(copy);
                }
            }

            return updated;
        });
    }

    // Inserts items at insertIndex by rebuilding the list in O(n) and raising a single Reset — the
    // same reason Remove/Move do: a raw mid-list ItemAdded makes the bound DataGridView shift/unshare
    // all rows, which is O(n^2) and hung for ~2 minutes on a single insert at 400K rows. do/undo/redo
    // all replace the whole list, so undo/redo also raise one Reset instead of a slow per-item event.
    private void InsertAll(int insertIndex, IReadOnlyList<HostsEntry> items)
    {
        if (items.Count == 0)
        {
            return;
        }

        var original = this.ToList();
        var index = insertIndex < 0 || insertIndex > original.Count ? original.Count : insertIndex;

        ReplaceAllUndoable(original, () =>
        {
            var updated = new List<HostsEntry>(original.Count + items.Count);
            updated.AddRange(original.Take(index));
            updated.AddRange(items);
            updated.AddRange(original.Skip(index));
            return updated;
        });
    }

    public void Remove(IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // Removing one-by-one is O(n²) for a large selection: each Remove(entry) is an O(n)
        // IndexOf plus an O(n) backing-store shift, and every removed row pushes its own undo
        // closure. Selecting-all then Remove/Cut on a huge file wedged the app for minutes.
        // Instead replace the whole list in O(n), backed by a single combined undo/redo action.
        // Own HashSet with the default (reference) comparer so membership can't be skewed by a
        // caller-supplied set built with a different comparer.
        var removeSet = new HashSet<HostsEntry>(entries);
        if (removeSet.Count == 0)
        {
            return;
        }

        var original = this.ToList();

        // No listed entry was actually present — nothing to do (and no spurious undo entry / event).
        if (!original.Any(removeSet.Contains))
        {
            return;
        }

        ReplaceAllUndoable(original, () => original.Where(entry => !removeSet.Contains(entry)).ToList());
    }

    // Registers a single undoable "replace the whole list" step and applies it, raising exactly one
    // ListChanged(Reset) for do / undo / redo. It retains only the pre-op snapshot (needed for undo);
    // the post-op list is rebuilt by buildUpdated (which closes over the small delta) rather than
    // snapshotted, so a bulk op holds O(original) references plus its delta, not two full copies.
    // MUST be called as a standalone operation, never nested inside an outer UndoManager.BatchActions:
    // the whole-list undo would clobber sibling mutations recorded in the same group.
    private void ReplaceAllUndoable(List<HostsEntry> original, Func<List<HostsEntry>> buildUpdated)
    {
        if (!UndoManager.Instance.IsCapturingSuspended)
        {
            UndoManager.Instance.AddActions(
                undoAction: () => ReplaceAll(original),
                redoAction: () => ReplaceAll(buildUpdated()));
        }

        ReplaceAll(buildUpdated());
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

        // Enumerated once by Where().ToList(); no need to pre-materialize the input.
        var changed = entries.Where(entry => entry.Enabled != isEnabled).ToList();
        if (changed.Count == 0)
        {
            return;
        }

        if (!UndoManager.Instance.IsCapturingSuspended)
        {
            UndoManager.Instance.AddActions(
                undoAction: () => ApplyEnabled(changed, !isEnabled),
                redoAction: () => ApplyEnabled(changed, isEnabled));
        }

        ApplyEnabled(changed, isEnabled);
    }

    // Toggles Enabled on each entry WITHOUT per-item PropertyChanged, then raises exactly one
    // ListChanged(Reset) via BatchUpdate. Enabling/disabling a large selection the naive way fired one
    // PropertyChanged per row; the bound Equin BindingListView reacts to each independently (O(n^2))
    // and hung ~2 min at 400K even with the grid detached. do/undo/redo all go through here, so undo
    // and redo are O(n) with a single Reset too. The classic grid rebuilds on the Reset; the modern UI
    // rebinds explicitly in its handler (its ListView won't see the silent per-item change otherwise).
    private void ApplyEnabled(IReadOnlyList<HostsEntry> entries, bool isEnabled) =>
        this.BatchUpdate(() =>
        {
            foreach (var entry in entries)
            {
                entry.SetEnabledSilently(isEnabled);
            }
        });

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
