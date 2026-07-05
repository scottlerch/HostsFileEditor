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
    /// Gets the selected host entries.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable<HostsEntry> SelectedHostEntries
    {
        get
        {
            // DataGridView.SelectedRows is O(n^2) for a large selection: its collection does an
            // O(n) duplicate check per added row, so Select-All then Cut/Remove on a huge hosts
            // file froze the app for minutes (~4.5 min at 400K rows). GetRowCount(Selected) is
            // O(1); when every visible row is selected — the common Select-All case — read the
            // bound view directly and skip SelectedRows entirely.
            //
            // Correctness of the fast path relies on two things: (1) the grid uses full-row
            // selection (RowHeaderSelect), so GetRowCount(Selected) counts whole selected rows;
            // (2) BoundEntryViews() yields exactly the visible (filtered/sorted) entries — see its
            // remarks. A selection is always a subset of the data rows, so `>= dataRowCount` can
            // only be true when every data row is genuinely selected (no false positive that would
            // delete unselected rows).
            var dataRowCount = RowCount - (NewRowIndex >= 0 ? 1 : 0);
            if (dataRowCount > 0 &&
                Rows.GetRowCount(DataGridViewElementStates.Selected) >= dataRowCount)
            {
                return BoundEntryViews()
                    .Where(view => view?.Object != null)
                    .Select(view => view!.Object);
            }

            return SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem as ObjectView<HostsEntry>)
                .Where(view => view?.Object != null)
                .Select(view => view!.Object);
        }

        set
        {
            foreach (var row in Rows.Cast<DataGridViewRow>())
            {
                if (row.DataBoundItem != null && row.Index < RowCount)
                {
                    row.Selected = row.DataBoundItem is ObjectView<HostsEntry> hostEntryView &&
                        value.Contains(hostEntryView.Object);
                }
            }
        }
    }

    /// <summary>
    /// Enumerates the bound <see cref="ObjectView{HostsEntry}"/> items in the current (filtered/
    /// sorted) view order, unwrapping the grid's <see cref="BindingSource"/> to its underlying list.
    /// Used to avoid the O(n^2) <see cref="DataGridView.SelectedRows"/> getter when all rows are
    /// selected. This is correct for an all-selected Cut/Delete under an active filter ONLY because
    /// the grid binds through a <c>BindingListView</c> (MainForm: <c>bindingSourceView.DataSource =
    /// _hostEntriesView</c>), so the unwrapped list yields exactly the visible filtered/sorted rows.
    /// If the binding topology ever exposes the unfiltered list here, this would operate on hidden
    /// rows.
    /// </summary>
    private IEnumerable<ObjectView<HostsEntry>> BoundEntryViews()
    {
        object? source = DataSource;
        while (source is BindingSource bindingSource)
        {
            source = bindingSource.List;
        }

        return (source as System.Collections.IEnumerable)?.OfType<ObjectView<HostsEntry>>()
            ?? [];
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
