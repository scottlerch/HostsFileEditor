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
    private int currentSortState = 0;

    /// <summary>
    /// Last sorted column.
    /// </summary>
    private DataGridViewColumn? lastSortedColumn = null;

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
            return SelectedRows
                .Cast<DataGridViewRow>()
                .Select(row => row.DataBoundItem as ObjectView<HostsEntry>)
                .Where(view => view?.Object != null)
                .Select(view => view!.Object);
        }

        set
        {
            foreach (DataGridViewRow row in Rows.Cast<DataGridViewRow>())
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
    /// Gets the current host entry.
    /// </summary>
    public HostsEntry? CurrentHostEntry => CurrentRow?.DataBoundItem is ObjectView<HostsEntry> view ? view.Object : null;

    /// <summary>
    /// Gets the last selected host entry.
    /// </summary>
    public HostsEntry? LastSelectedHostEntry => SelectedHostEntries.LastOrDefault();

    /// <summary>
    /// Gets the first selected host entry.
    /// </summary>
    public HostsEntry? FirstSelectedHostEntry => SelectedHostEntries.FirstOrDefault();

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
        HostsEntry? entry = viewObject?.Object;

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

        if (SortedColumn == lastSortedColumn)
        {
            currentSortState++;

            // After sorting twice (ascending then descending) clear the sort
            if (currentSortState > 2)
            {
                BeginInvoke(
                    (MethodInvoker)delegate ()
                    {
                        Application.DoEvents();
                        ClearSort?.Invoke();
                    });

                currentSortState = 0;
                lastSortedColumn = null;
            }
        }
        else
        {
            currentSortState = 1;
            lastSortedColumn = Columns[e.ColumnIndex];
        }
    }
}
