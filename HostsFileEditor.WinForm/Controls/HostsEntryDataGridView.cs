using Equin.ApplicationFramework;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace HostsFileEditor.Controls;

/// <summary>
/// DataGridView class for HostsEntry.
/// </summary>
internal sealed class HostsEntryDataGridView : DataGridView
{
    // Equin's public RemoveSort() does NOT restore source order: it installs a SortComparer over an
    // EMPTY sort-description collection whose Compare always returns 0, and FilterAndSort still runs
    // List.Sort with it — .NET's unstable introsort then PERMUTES the all-equal sequence, so the
    // "unsorted" view shows a scrambled order that no longer matches what Save writes. The only way
    // to truly clear the sort is to null the view's private comparer and rebuild: these members are
    // resolved once from the (dead, unchanging) Equin 1.4.5222.35545 assembly. The classic app is
    // published without trimming, so private reflection is safe here. Null members = fall back to
    // RemoveSort (scrambled beats broken).
    private static readonly FieldInfo? ViewSortsField = FindViewMember(t => t.GetField("_sorts", BindingFlags.Instance | BindingFlags.NonPublic));
    private static readonly FieldInfo? ViewComparerField = FindViewMember(t => t.GetField("_comparer", BindingFlags.Instance | BindingFlags.NonPublic));
    private static readonly MethodInfo? ViewFilterAndSortMethod = FindViewMember(t => t.GetMethod("FilterAndSort", BindingFlags.Instance | BindingFlags.NonPublic));

    private static T? FindViewMember<T>(Func<Type, T?> lookup)
        where T : MemberInfo
    {
        for (var type = typeof(BindingListView<HostsEntry>); type is not null; type = type.BaseType)
        {
            if (lookup(type) is { } member)
            {
                return member;
            }
        }

        return null;
    }

    /// <summary>
    /// The column the grid is currently sorted by, or <see langword="null"/> when unsorted. Tracked
    /// here because sorting is applied programmatically (see <see cref="OnColumnAdded"/>), so the
    /// framework's <see cref="DataGridView.SortedColumn"/> / <see cref="DataGridView.SortOrder"/>
    /// stay unset.
    /// </summary>
    private DataGridViewColumn? _sortColumn;

    /// <summary>
    /// The direction of the current sort, or <see cref="SortOrder.None"/> when unsorted.
    /// </summary>
    private SortOrder _sortDirection = SortOrder.None;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsEntryDataGridView"/> class.
    /// </summary>
    public HostsEntryDataGridView()
    {
        AllowUserToResizeRows = false;
    }

    /// <summary>
    /// Gets the current sort direction, or <see cref="SortOrder.None"/> when the grid is unsorted.
    /// Read instead of the framework's <see cref="DataGridView.SortOrder"/>, which is not maintained
    /// because sorting is applied programmatically through the bound view's external comparer.
    /// </summary>
    public SortOrder ActiveSortOrder => _sortDirection;

    /// <summary>
    /// Above this many rows, the selection-restore setter restores nothing (and nulls the current
    /// cell): each <c>Rows[i].Selected = true</c> walks the grid's internal selected-band list
    /// (O(selected-so-far)), so restoring a huge selection is O(k^2) inside the framework —
    /// minutes at 400K — no matter how cheaply the rows are matched.
    /// </summary>
    private const int MaxSelectionRestoreCount = 20000;

    /// <summary>
    /// Gets the selected host entries.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable<HostsEntry> SelectedHostEntries
    {
        get
        {
            // One O(n) pass for ANY selection shape: walk the selected row indices with
            // GetFirstRow/GetNextRow and map them positionally onto the bound view. This replaces
            // two buggy branches: DataGridView.SelectedRows (O(k^2) collection build for a large
            // selection — the original ~4.5-minute Select-All hang — AND it enumerates in
            // reverse-of-selection order, so a partial-selection Copy->Paste reinserted the block
            // reversed), and a count-based "all selected" fast path whose guard miscounted when the
            // new-row placeholder was selected (Ctrl+A also selects the placeholder, so Ctrl+A then
            // deselect-one still passed `>= dataRowCount` and returned EVERY row — deleting a row
            // the user had excluded). The placeholder is now skipped explicitly, and the result is
            // always in visible row order.
            //
            // Positional mapping is correct because the grid binds through the BindingListView
            // (MainForm: bindingSourceView.DataSource = _hostEntriesView), so the unwrapped list is
            // exactly the visible (filtered/sorted) rows in grid row order, with the placeholder
            // (never part of the view) last.
            //
            // Materialized eagerly: callers snapshot the selection, then ClearSelection() or mutate
            // the bound list before enumerating. A deferred sequence would re-read the changed
            // grid/list and yield wrong rows or throw "collection modified".
            var result = new List<HostsEntry>();
            if (BoundView() is not { } views)
            {
                return result;
            }

            for (var rowIndex = Rows.GetFirstRow(DataGridViewElementStates.Selected);
                 rowIndex >= 0;
                 rowIndex = Rows.GetNextRow(rowIndex, DataGridViewElementStates.Selected))
            {
                if (rowIndex == NewRowIndex || rowIndex >= views.Count)
                {
                    continue;
                }

                if (views[rowIndex] is { Object: { } entry })
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        set
        {
            // Restore a selection cheaply on a huge grid: clear once, then select ONLY the matched
            // rows by their view index (view order == grid row order; only matched rows get realized
            // via Rows[index]). Membership uses our own reference-comparer set — like Core's Remove —
            // so a caller-supplied set with a value-based comparer can't match the wrong duplicate
            // row. Restores beyond MaxSelectionRestoreCount restore nothing at all (see below and
            // the constant's remarks for why full restores are O(k^2) inside the framework).
            var selected = new HashSet<HostsEntry>(value);
            ClearSelection();
            if (selected.Count == 0 || BoundView() is not { } views)
            {
                return;
            }

            if (selected.Count > MaxSelectionRestoreCount)
            {
                // Restore NOTHING above the cap (an honest empty selection), and null the current
                // cell too: with the selection gone, the command handlers fall through to their
                // CurrentHostEntry fallback, so a survived current row would make the user's next
                // repeat gesture (e.g. Alt+Up after moving a >20K block) silently act on ONE row —
                // tearing it out of the block they believe is still selected. The earlier behavior
                // of selecting just the first matched row caused exactly that corruption.
                CurrentCell = null;
                return;
            }

            var count = Math.Min(views.Count, RowCount);
            for (var index = 0; index < count; index++)
            {
                if (views[index] is { Object: { } entry } && selected.Contains(entry))
                {
                    Rows[index].Selected = true;
                }
            }
        }
    }

    /// <summary>
    /// Snapshots the selected entries and clears the selection in one call. Every bulk mutation
    /// must clear BEFORE raising the list Reset: reconciling a huge selection against a Reset is a
    /// posted O(n^2) operation inside the grid (it froze the UI for minutes at 400K rows). Owning
    /// the sequence here keeps that hazard knowledge in one place instead of five call sites.
    /// </summary>
    public List<HostsEntry> TakeSelectedHostEntries()
    {
        var selected = SelectedHostEntries.ToList();
        ClearSelection();
        return selected;
    }

    /// <summary>
    /// Unwraps the grid's <see cref="BindingSource"/> chain to the bound Equin
    /// <see cref="BindingListView{T}"/> — the sortable view whose items are the visible
    /// (filtered/sorted) rows in grid order, index-aligned with the grid's data rows. The
    /// <see cref="SelectedHostEntries"/> getter/setter rely on that index alignment (if the binding
    /// topology ever exposes the unfiltered list here they would operate on hidden rows), and the
    /// sort methods use it to apply/clear the external comparer — one unwrap so the two can never
    /// disagree about which list they address.
    /// </summary>
    private BindingListView<HostsEntry>? BoundView()
    {
        var source = DataSource;
        while (source is BindingSource bindingSource)
        {
            source = bindingSource.List;
        }

        return source as BindingListView<HostsEntry>;
    }

    /// <summary>
    /// Gets the number of selected rows in O(1). The framework's <see cref="DataGridView.SelectedRows"/>
    /// getter is O(n^2) for a large selection (its collection does a duplicate check per row as it is
    /// built), so <c>SelectedRows.Count</c> alone freezes the app on a huge hosts file — every command
    /// guard must use this instead.
    /// </summary>
    public int SelectedRowCount => Rows.GetRowCount(DataGridViewElementStates.Selected);

    /// <summary>
    /// Gets the current host entry.
    /// </summary>
    public HostsEntry? CurrentHostEntry => CurrentRow?.DataBoundItem is ObjectView<HostsEntry> view ? view.Object : null;

    /// <summary>
    /// Gets the host entry bound to the given row index, or <see langword="null"/> if
    /// the index is out of range or the row has no bound entry.
    /// </summary>
    public HostsEntry? GetHostEntry(int rowIndex) =>
        rowIndex >= 0 && rowIndex < Rows.Count && Rows[rowIndex].DataBoundItem is ObjectView<HostsEntry> view
            ? view.Object
            : null;

    /// <inheritdoc />
    protected override void OnCurrentCellDirtyStateChanged(System.EventArgs e)
    {
        base.OnCurrentCellDirtyStateChanged(e);

        // Immediately commit check changes
        if (CurrentCell?.GetType() == typeof(DataGridViewCheckBoxCell))
        {
            CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    /// <inheritdoc />
    protected override void OnCellFormatting(DataGridViewCellFormattingEventArgs e)
    {
        var viewObject = Rows[e.RowIndex].DataBoundItem as ObjectView<HostsEntry>;
        var entry = viewObject?.Object;

        if (entry != null)
        {
            if (!entry.Enabled && entry.Valid)
            {
                e.CellStyle.BackColor = Color.LightGray;
            }
            else if (!entry.Enabled)
            {
                e.CellStyle.BackColor = Color.Gray;
                e.CellStyle.ForeColor = Color.White;
            }
            else
            {
                e.CellStyle.BackColor = !entry.Valid ? Color.LightPink : Color.White;
            }
        }

        base.OnCellFormatting(e);
    }

    /// <summary>
    /// Forces programmatic sorting on every sortable column. The grid binds through an Equin
    /// <see cref="BindingListView{T}"/> whose automatic (property-based) sort emits a
    /// <c>Reflection.Emit</c> comparer; the .NET 10 JIT rejects that IL (<see cref="BadImageFormatException"/>
    /// "Bad IL format"), so the framework's built-in header sort crashes. Programmatic mode suppresses
    /// that path — we sort via an external, non-emitting comparer in <see cref="ApplyColumnSort"/>.
    /// </summary>
    protected override void OnColumnAdded(DataGridViewColumnEventArgs e)
    {
        if (e.Column.SortMode == DataGridViewColumnSortMode.Automatic)
        {
            e.Column.SortMode = DataGridViewColumnSortMode.Programmatic;
        }

        base.OnColumnAdded(e);
    }

    /// <summary>
    /// Re-flips any later assignment of <see cref="DataGridViewColumnSortMode.Automatic"/> back to
    /// Programmatic. <see cref="OnColumnAdded"/> alone is NOT enough: the designer assigns
    /// <c>SortMode = Automatic</c> to the two checkbox columns AFTER <c>Columns.AddRange</c> (their
    /// add-time default is NotSortable, so the add-time flip never fires), which left the .NET 10
    /// Equin sort crash live on the Valid/Enabled headers. This hook makes the invariant hold for
    /// any assignment order, including future designer regeneration. The re-entrant second change
    /// event (Automatic -> Programmatic) is a no-op under the guard.
    /// </summary>
    protected override void OnColumnSortModeChanged(DataGridViewColumnEventArgs e)
    {
        if (e.Column.SortMode == DataGridViewColumnSortMode.Automatic)
        {
            e.Column.SortMode = DataGridViewColumnSortMode.Programmatic;
        }

        base.OnColumnSortModeChanged(e);
    }

    /// <inheritdoc />
    protected override void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e)
    {
        base.OnColumnHeaderMouseClick(e);

        // The framework's automatic sort was left-click-only; keep that contract. Without this,
        // right-clicking a header to open the grid context menu would also cycle the sort (an
        // O(n log n) reorder plus a Reset at 400K rows) and middle clicks would too.
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (e.ColumnIndex < 0 || e.ColumnIndex >= Columns.Count)
        {
            return;
        }

        var column = Columns[e.ColumnIndex];

        // Only sortable data columns participate; skip filler / non-data columns.
        if (column.SortMode != DataGridViewColumnSortMode.Programmatic ||
            string.IsNullOrEmpty(column.DataPropertyName))
        {
            return;
        }

        // Cycle the same column ascending -> descending -> unsorted (the classic 3-click behavior);
        // a different column starts a fresh ascending sort.
        if (column == _sortColumn)
        {
            if (_sortDirection == SortOrder.Ascending)
            {
                ApplyColumnSort(column, SortOrder.Descending);
            }
            else
            {
                ClearColumnSort();
            }
        }
        else
        {
            ApplyColumnSort(column, SortOrder.Ascending);
        }
    }

    /// <summary>
    /// Sorts the bound view by <paramref name="column"/> in the given direction using an external
    /// (non-emitting) comparer, then updates the header sort glyph ourselves (programmatic sort mode
    /// does not) and notifies listeners.
    /// </summary>
    private void ApplyColumnSort(DataGridViewColumn column, SortOrder direction)
    {
        if (BoundView() is not { } view)
        {
            return;
        }

        // Commit an in-progress cell edit BEFORE the sort's Reset, exactly like the framework's
        // automatic sort did (its SortInternal validates the current cell first): the Reset tears
        // the editing control down with no commit anywhere in that path, silently discarding the
        // typed value. Mirror the framework and bail out if validation fails.
        if (!EndEdit())
        {
            return;
        }

        view.ApplySort(ComparerFor(column.DataPropertyName, direction));

        if (_sortColumn is not null && _sortColumn != column)
        {
            _sortColumn.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        column.HeaderCell.SortGlyphDirection = direction;
        _sortColumn = column;
        _sortDirection = direction;
        OnSorted(EventArgs.Empty);
    }

    /// <summary>
    /// Removes any active sort from the bound view — genuinely restoring FILE order — clears the
    /// header glyph, and raises <see cref="DataGridView.Sorted"/> so sort-dependent UI (e.g. Move
    /// Up/Down) refreshes. A no-op when no sort is active, so the Remove Sort menu can't disturb an
    /// unsorted view (Equin's RemoveSort is NOT harmless when idle — see RestoreSourceOrder).
    /// </summary>
    public void ClearColumnSort()
    {
        // Nothing to clear: skip the view work entirely. Important beyond perf — calling Equin's
        // RemoveSort on an unsorted view would INSTALL its all-equal comparer and scramble (below).
        if (_sortColumn is null)
        {
            return;
        }

        // Same contract as ApplyColumnSort: commit the in-progress edit before the Reset.
        if (!EndEdit())
        {
            return;
        }

        if (BoundView() is { } view)
        {
            RestoreSourceOrder(view);
        }

        _sortColumn.HeaderCell.SortGlyphDirection = SortOrder.None;
        _sortColumn = null;
        _sortDirection = SortOrder.None;
        OnSorted(EventArgs.Empty);
    }

    /// <summary>
    /// Truly un-sorts the view. Equin's own <c>RemoveSort()</c> replaces the comparer with a
    /// SortComparer over an EMPTY description collection whose Compare always returns 0 — and
    /// FilterAndSort still runs List.Sort with it, whose unstable introsort PERMUTES an all-equal
    /// sequence: the "unsorted" grid showed a scrambled order that re-scrambled on every Reset and
    /// no longer matched what Save writes. Fix: mirror RemoveSort's state transitions but null the
    /// private comparer (sorting is skipped entirely when it is null) and rebuild once — the view
    /// returns to source (file) order and stays there. Falls back to plain RemoveSort if the
    /// reflected members are missing (wrong-but-alive beats broken).
    /// </summary>
    private static void RestoreSourceOrder(BindingListView<HostsEntry> view)
    {
        if (ViewSortsField is null || ViewComparerField is null || ViewFilterAndSortMethod is null)
        {
            view.RemoveSort();
            return;
        }

        ViewSortsField.SetValue(view, new ListSortDescriptionCollection());
        ViewComparerField.SetValue(view, null);
        ViewFilterAndSortMethod.Invoke(view, null);
    }

    /// <summary>
    /// Builds an emit-free, allocation-free comparer over <see cref="HostsEntry"/> for the given
    /// bound property and direction. Direct TYPED property access avoids reflection, the
    /// Equin/.NET 10 emit crash, and per-comparison boxing (the earlier <c>Comparer&lt;object&gt;</c>
    /// variant boxed ~15M bools per header click on a 400K-row checkbox sort). String columns use
    /// culture-sensitive comparison for parity with the framework's original property sort
    /// (which routed through <see cref="string.CompareTo(string)"/>).
    /// </summary>
    private static Comparer<HostsEntry> ComparerFor(string dataPropertyName, SortOrder direction)
    {
        var comparer = dataPropertyName switch
        {
            nameof(HostsEntry.Enabled) => Comparer<HostsEntry>.Create(static (x, y) => x.Enabled.CompareTo(y.Enabled)),
            nameof(HostsEntry.Valid) => Comparer<HostsEntry>.Create(static (x, y) => x.Valid.CompareTo(y.Valid)),
            nameof(HostsEntry.IpAddress) => Comparer<HostsEntry>.Create(static (x, y) => string.Compare(x.IpAddress, y.IpAddress, StringComparison.CurrentCulture)),
            nameof(HostsEntry.HostNames) => Comparer<HostsEntry>.Create(static (x, y) => string.Compare(x.HostNames, y.HostNames, StringComparison.CurrentCulture)),
            nameof(HostsEntry.Comment) => Comparer<HostsEntry>.Create(static (x, y) => string.Compare(x.Comment, y.Comment, StringComparison.CurrentCulture)),
            nameof(HostsEntry.UnparsedText) => Comparer<HostsEntry>.Create(static (x, y) => string.Compare(x.UnparsedText, y.UnparsedText, StringComparison.CurrentCulture)),
            _ => UnknownColumnComparer(dataPropertyName),
        };

        return direction == SortOrder.Descending
            ? Comparer<HostsEntry>.Create((x, y) => comparer.Compare(y, x))
            : comparer;
    }

    /// <summary>
    /// Loud fallback for a column property this switch does not know: a silently "working" fallback
    /// (the old <c>ToString()</c> concatenation) would make a future column compile clean and sort
    /// by IP+hostnames+comment with no error — wrong order discovered only by users. Debug builds
    /// fail fast; release keeps the grid alive with the documented approximation.
    /// </summary>
    private static Comparer<HostsEntry> UnknownColumnComparer(string dataPropertyName)
    {
        Debug.Fail($"No sort comparer for column property '{dataPropertyName}' — add a case to {nameof(ComparerFor)}.");
        return Comparer<HostsEntry>.Create(static (x, y) => string.Compare(x.ToString(), y.ToString(), StringComparison.CurrentCulture));
    }
}
