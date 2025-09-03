using Equin.ApplicationFramework;

namespace HostsFileEditor.Controls;

/// <summary>
/// DataGridView class for use with HostsArchive objects.
/// </summary>
internal sealed class HostsArchiveDataGridView : DataGridView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HostsArchiveDataGridView"/> class.
    /// </summary>
    public HostsArchiveDataGridView()
    {
        AllowUserToResizeRows = false;
        AllowUserToResizeColumns = false;
        AllowUserToDeleteRows = false;
        AllowDrop = false;
        AllowUserToAddRows = false;
        AllowUserToOrderColumns = false;
    }

    /// <summary>
    /// Gets the current hosts archive.
    /// </summary>
    public HostsArchive? CurrentHostsArchive
    {
        get
        {
            HostsArchive? archive = null;

            if (CurrentRow != null)
            {
                var view = CurrentRow.DataBoundItem as ObjectView<HostsArchive>;
                archive = view?.Object;
            }

            return archive;
        }
    }
}
