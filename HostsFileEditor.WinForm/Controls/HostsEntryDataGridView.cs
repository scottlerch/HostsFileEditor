using Equin.ApplicationFramework;
using System.ComponentModel;

namespace HostsFileEditor.Controls;

/// <summary>
/// DataGridView class for HostsEntry.
/// </summary>
internal sealed class HostsEntryDataGridView : DataGridView
{
    /// <summary>
    /// Current sort state used to determine when to remove sort.
    /// </summary>
    private int _currentSortState;

    /// <summary>
    /// Last sorted column.
    /// </summary>
    private DataGridViewColumn? _lastSortedColumn;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsEntryDataGridView"/> class.
    /// </summary>
    public HostsEntryDataGridView()
    {
        AllowUserToResizeRows = false;
        ClearSort = () => { }; // Initialize with empty action
    }

    /// <summary>
    /// Gets or sets the action to clear the sort of the underlying 
    /// data source.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Action ClearSort { get; set; }

    /// <summary>
    /// Above this many rows, the selection-restore setter highlights only the first matched row
    /// instead of the whole set: each <c>Rows[i].Selected = true</c> walks the grid's internal
    /// selected-band list (O(selected-so-far)), so restoring a huge selection is O(k^2) inside the
    /// framework — minutes at 400K — no matter how cheaply the rows are matched.
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
            if (BoundViewList() is not { } views)
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

                if (views[rowIndex] is ObjectView<HostsEntry> { Object: { } entry })
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
            // row. Restores beyond MaxSelectionRestoreCount select just the first matched row (see
            // the constant's remarks for why full restores are O(k^2) inside the framework).
            var selected = new HashSet<HostsEntry>(value);
            ClearSelection();
            if (selected.Count == 0 || BoundViewList() is not { } views)
            {
                return;
            }

            var restoreAll = selected.Count <= MaxSelectionRestoreCount;
            var count = Math.Min(views.Count, RowCount);
            for (var index = 0; index < count; index++)
            {
                if (views[index] is ObjectView<HostsEntry> { Object: { } entry } && selected.Contains(entry))
                {
                    Rows[index].Selected = true;

                    if (!restoreAll)
                    {
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Unwraps the grid's <see cref="BindingSource"/> chain to the underlying bound list — the
    /// <c>BindingListView</c> whose items are <see cref="ObjectView{HostsEntry}"/> in the current
    /// (filtered/sorted) VISIBLE order, index-aligned with the grid's data rows. Both the
    /// <see cref="SelectedHostEntries"/> getter and setter rely on that index alignment; if the
    /// binding topology ever exposes the unfiltered list here, they would operate on hidden rows.
    /// </summary>
    private System.Collections.IList? BoundViewList()
    {
        var source = DataSource;
        while (source is BindingSource bindingSource)
        {
            source = bindingSource.List;
        }

        return source as System.Collections.IList;
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

    /// <inheritdoc />
    protected override void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e)
    {
        base.OnColumnHeaderMouseClick(e);

        if (SortedColumn == _lastSortedColumn)
        {
            _currentSortState++;

            // After sorting twice (ascending then descending) clear the sort
            if (_currentSortState > 2)
            {
                BeginInvoke(
                    (MethodInvoker)delegate ()
                    {
                        Application.DoEvents();
                        ClearSort?.Invoke();
                    });

                _currentSortState = 0;
                _lastSortedColumn = null;
            }
        }
        else
        {
            _currentSortState = 1;
            _lastSortedColumn = Columns[e.ColumnIndex];
        }
    }
}
