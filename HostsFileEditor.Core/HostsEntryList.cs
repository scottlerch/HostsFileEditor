using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace HostsFileEditor;

// IL3050 (type level): ILC attributes one Enum.GetValues(Type) reach to the TYPE itself (a
// synthesized member of the BindingList<T> instantiation, not any method we declare), so it cannot
// be suppressed per-method. Scope is still just this class — the trim/AOT analyzers stay armed for
// the rest of the AOT-published app. The reflective descriptor machinery is unreachable at runtime:
// nothing data-binds to this list through PropertyDescriptors (the WinUI app rebinds an
// ObservableCollection; WinForms binds through Equin's view).
[UnconditionalSuppressMessage("AOT", "IL3050", Justification = "BindingList descriptor machinery unreachable; see type comment.")]
public class HostsEntryList : BindingList<HostsEntry>
{
    public static readonly string[] DefaultLines = Resources.hosts.Split(
        [Environment.NewLine],
        StringSplitOptions.None);

    /// <summary>
    /// Above this many selected rows, both UIs DROP the selection on a bulk rebind rather than
    /// restore it: re-establishing a huge native selection is O(k^2) in each framework (WinForms'
    /// selected-band list, WinUI's SelectedItems vector). Shared so the two editions' "huge
    /// selection" boundary can't silently drift apart (classic's MaxSelectionRestoreCount and modern's
    /// LogicalSelectAllThreshold both derive from it).
    /// </summary>
    public const int HugeSelectionThreshold = 20_000;

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

    /// <summary>
    /// Merges the entries parsed from <paramref name="lines"/> (another hosts file) into this list,
    /// eliminating duplicates (issue #26). Every <b>valid</b> incoming entry whose identity — its IP
    /// address plus parser-normalized host names, compared case-insensitively — is not already present
    /// is appended in order; entries that duplicate an existing one, comment-only lines, and blank
    /// lines are skipped. Enabled-vs-disabled is intentionally ignored when comparing (the same
    /// mapping present twice, once commented out, is still a duplicate), so the existing copy wins.
    /// Returns the number of entries actually added. Undo/redo is suspended for the bulk add; the
    /// caller clears history (post-merge indices invalidate prior undo actions) and typically wraps
    /// this in a <c>BatchUpdate</c> so the bound view resets once.
    /// </summary>
    public int MergeLines(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in this)
        {
            if (entry.Valid)
            {
                identities.Add(IdentityOf(entry));
            }
        }

        var toAdd = new List<HostsEntry>();
        foreach (var line in lines)
        {
            var entry = new HostsEntry(line);

            // Merge only real host entries; skip comment-only lines and blanks from the incoming file.
            if (!entry.Valid)
            {
                continue;
            }

            // HashSet.Add is false when the identity is already present (existing OR earlier in this
            // same merge), so the first occurrence wins and later duplicates are dropped.
            if (identities.Add(IdentityOf(entry)))
            {
                toAdd.Add(entry);
            }
        }

        if (toAdd.Count > 0)
        {
            UndoManager.Instance.SuspendUndoRedo(() =>
            {
                foreach (var entry in toAdd)
                {
                    Add(entry);
                }
            });
        }

        return toAdd.Count;

        static string IdentityOf(HostsEntry entry) => $"{entry.IpAddress}\t{entry.HostNames}";
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
    // O(n^2) hang left in the app on a large multi-row move. Moving rows keep their relative order.
    // Anchoring on a row INSIDE the moving set is a true no-op — no reorder, no Reset, no undo step,
    // no IsModified change. (Both UIs' Move Up/Down anchor on the row just outside the selection, so
    // they never hit this; the contract exists for future callers such as drag-and-drop onto a row
    // within the dragged selection.)
    private void MoveRelative(IEnumerable<HostsEntry> entries, HostsEntry anchor, bool insertAfterAnchor)
    {
        var moveSet = new HashSet<HostsEntry>(entries);
        if (moveSet.Count == 0 || moveSet.Contains(anchor))
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

    // Inserts the entries before the anchor. A null (or not-found) anchor appends to the end — that
    // is the paste contract both UIs share: paste at the current row when there is one, otherwise
    // append (e.g. into the empty list after Cut-All, or with nothing selected). Owning the fallback
    // here keeps the two UIs from drifting apart on where an unanchored paste lands.
    public void Insert(HostsEntry? entry, IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var index = entry is null ? Count : IndexOf(entry);
        InsertAll(index < 0 ? Count : index, SnapshotOf(entries));
    }

    // Appends entries at the end.
    public void InsertRange(IEnumerable<HostsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        InsertAll(Count, SnapshotOf(entries));
    }

    // Always copies the caller's collection: InsertAll captures the items into a redo closure that
    // re-runs later, so aliasing a caller-owned list (e.g. a clipboard buffer that gets cleared and
    // refilled) would silently change what a redo re-inserts.
    private static List<HostsEntry> SnapshotOf(IEnumerable<HostsEntry> entries) => [.. entries];

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
    private void InsertAll(int insertIndex, List<HostsEntry> items)
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

    // Applies a single undoable "replace the whole list" step, raising exactly one
    // ListChanged(Reset) for do / undo / redo. It retains only the pre-op snapshot (needed for undo);
    // the post-op list is rebuilt by buildUpdated (which closes over the small delta) rather than
    // snapshotted, so a bulk op holds O(original) references plus its delta, not two full copies.
    // The undo/redo pair is registered only AFTER the mutation succeeds, so a throw mid-rebuild
    // (OOM, a ListChanged handler failing) cannot leave a history entry whose captured snapshot no
    // longer matches the live list.
    // MUST be called as a standalone operation, never nested inside an outer UndoManager.BatchActions:
    // the whole-list undo would clobber sibling mutations recorded in the same group.
    private void ReplaceAllUndoable(List<HostsEntry> original, Func<List<HostsEntry>> buildUpdated)
    {
        ReplaceAll(buildUpdated());

        if (!UndoManager.Instance.IsCapturingSuspended)
        {
            UndoManager.Instance.AddActions(
                undoAction: () => ReplaceAll(original),
                redoAction: () => ReplaceAll(buildUpdated()));
        }
    }

    // Replaces the entire list content in O(n): ClearItems unhooks every row's change tracking,
    // then the rows are re-added (re-hooking survivors). Wrapped in BatchUpdate so bound views get
    // exactly one ListChanged(Reset). The SuspendUndoRedo wrapper is purely defensive — the
    // base.InsertItem/ClearItems calls bypass the capturing overrides, so nothing here records undo
    // even without it — but it makes the "callers own the combined undo/redo action" contract
    // explicit and future-proofs against a mutation being routed through a capturing path.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList only used for basic add/remove/change notifications; PropertyDescriptor reflective paths not used.")]
    private void ReplaceAll(List<HostsEntry> target)
    {
        this.BatchUpdate(() =>
            UndoManager.Instance.SuspendUndoRedo(() =>
            {
                ClearItems();
                foreach (var entry in target)
                {
                    base.InsertItem(Count, entry);
                }
            }));
    }

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

        // Mutate first, then register: a throw mid-apply must not leave a history entry (same
        // ordering rationale as ReplaceAllUndoable).
        ApplyEnabled(changed, isEnabled);

        if (!UndoManager.Instance.IsCapturingSuspended)
        {
            UndoManager.Instance.AddActions(
                undoAction: () => ApplyEnabled(changed, !isEnabled),
                redoAction: () => ApplyEnabled(changed, isEnabled));
        }
    }

    // Toggles Enabled on each entry WITHOUT per-item PropertyChanged, then raises exactly one
    // ListChanged(Reset) via BatchUpdate. Enabling/disabling a large selection the naive way fired one
    // PropertyChanged per row; the bound Equin BindingListView reacts to each independently (O(n^2))
    // and hung ~2 min at 400K even with the grid detached. do/undo/redo all go through here, so undo
    // and redo are O(n) with a single Reset too. The classic grid rebuilds on the Reset; the modern UI
    // rebinds explicitly in its handler (its ListView won't see the silent per-item change otherwise).
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList only used for basic add/remove/change notifications; PropertyDescriptor reflective paths not used.")]
    private void ApplyEnabled(IReadOnlyList<HostsEntry> entries, bool isEnabled)
    {
        this.BatchUpdate(() =>
        {
            foreach (var entry in entries)
            {
                entry.SetEnabledSilently(isEnabled);
            }
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
