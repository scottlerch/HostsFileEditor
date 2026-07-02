using Equin.ApplicationFramework;
using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.Text;

namespace HostsFileEditor;

/// <summary>
/// The main form for the application.
/// </summary>
internal sealed partial class MainForm : Form
{
    /// <summary>
    /// The filter.
    /// </summary>
    private HostsFilter? _filter;

    /// <summary>
    /// The host entries view.
    /// </summary>
    private BindingListView<HostsEntry>? _hostEntriesView;

    /// <summary>
    /// The hosts archive view.
    /// </summary>
    private BindingListView<HostsArchive>? _hostsArchiveView;

    /// <summary>
    /// The clipboard host entries.
    /// </summary>
    private IEnumerable<HostsEntry>? _clipboardEntries;

    /// <summary>
    /// Determines if user is currently adding a new row.  Used for ugly
    /// hacks setup in load event.
    /// </summary>
    private bool _addingNew;

    /// <summary>
    /// Ignore adding new in progress. Used for ugly hacks setup in load 
    /// event.
    /// </summary>
    private bool _ignoreAddingNew;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainForm"/> class.
    /// </summary>
    public MainForm()
    {
        InitializeComponent();

        saveFileDialog.InitialDirectory = HostsFile.DefaultHostFilePath;

        // Prevent data binding from setting properties to null when
        // an empty string is typed in
        columnComment.DefaultCellStyle.NullValue = null;
        columnIpAddress.DefaultCellStyle.NullValue = null;
        columnHostnames.DefaultCellStyle.NullValue = null;
    }

    /// <inheritdoc />
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == ProgramSingleInstance.WmShowFirstInstance)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                WindowState = FormWindowState.Normal;
            }

            this.ShowOrActivate();
        }

        base.WndProc(ref message);
    }

    /// <summary>
    /// Called when archive clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance
    /// containing the event data.</param>
    private void OnArchiveClick(object sender, EventArgs e)
    {
        dataGridViewHostsEntries.CommitEdit(
            DataGridViewDataErrorContexts.Commit);

        using var inputDialog = new InputForm();
        inputDialog.Text = Text;
        inputDialog.Prompt = Resources.InputArchivePrompt;

        var result = inputDialog.ShowDialog(this);

        if (result == DialogResult.OK)
        {
            HostsFile.Instance.Archive(inputDialog.Input);
        }
    }

    /// <summary>
    /// Occurs when copy clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnCopyClick(object sender, EventArgs e)
    {
        // HACK: If editing cell forward cut/copy/paste command
        // to editing control
        if (dataGridViewHostsEntries.IsCurrentCellInEditMode)
        {
            var keys = menuCopy.ShortcutKeys;
            menuCopy.ShortcutKeys = Keys.None;
            menuContextCopy.ShortcutKeys = Keys.None;
            SendKeys.SendWait("^(C)");
            menuCopy.ShortcutKeys = keys;
            menuContextCopy.ShortcutKeys = keys;
            return;
        }

        if (dataGridViewHostsEntries.SelectedRows.Count > 0)
        {
            _clipboardEntries = [.. dataGridViewHostsEntries.SelectedHostEntries.Select(entry => new HostsEntry(entry))];
        }
        else
        {
            StringBuilder builder = new();

            foreach (
                DataGridViewCell cell in
                dataGridViewHostsEntries.SelectedCells)
            {
                if (cell.ValueType == typeof(string))
                {
                    builder.Append(cell.Value?.ToString());
                }
            }

            Clipboard.SetText(builder.ToString());
        }
    }

    /// <summary>
    /// Occurs when cut clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnCutClick(object sender, EventArgs e)
    {
        // HACK: If editing cell forward cut/copy/paste command
        // to editing control
        if (dataGridViewHostsEntries.IsCurrentCellInEditMode)
        {
            var keys = menuCut.ShortcutKeys;
            menuCut.ShortcutKeys = Keys.None;
            menuContextCut.ShortcutKeys = Keys.None;
            SendKeys.SendWait("^(X)");
            menuCut.ShortcutKeys = keys;
            menuContextCut.ShortcutKeys = keys;
            return;
        }

        dataGridViewHostsEntries.CancelEdit();

        if (dataGridViewHostsEntries.SelectedRows.Count > 0)
        {
            // Clone like Copy does, so the clipboard holds independent entries rather
            // than the live instances being removed (keeps undo/paste state consistent).
            _clipboardEntries = [.. dataGridViewHostsEntries.SelectedHostEntries.Select(entry => new HostsEntry(entry))];

            HostsFile.Instance.Entries.Remove(_clipboardEntries);
        }
        else
        {
            StringBuilder builder = new();

            foreach (
                DataGridViewCell cell in
                dataGridViewHostsEntries.SelectedCells)
            {
                if (cell.ValueType == typeof(string))
                {
                    builder.Append(cell.Value?.ToString());
                    cell.Value = string.Empty;
                }
            }

            Clipboard.SetText(builder.ToString());
        }
    }

    /// <summary>
    /// Occurs when delete clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnDeleteClick(object sender, EventArgs e)
    {
        if (dataGridViewHostsEntries.SelectedRows.Count > 0)
        {
            HostsFile.Instance.Entries.Remove(
                dataGridViewHostsEntries.SelectedHostEntries);
        }
        else
        {
            foreach (
                DataGridViewCell cell in
                dataGridViewHostsEntries.SelectedCells)
            {
                if (cell.ValueType == typeof(string))
                {
                    cell.Value = string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Occurs when duplicate clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnDuplicateClick(object sender, EventArgs e)
    {
        if (dataGridViewHostsEntries.SelectedRows.Count > 0)
        {
            // Snapshot the selection first: InsertAfter mutates the bound list (and
            // thus SelectedRows) while we enumerate, which would otherwise throw.
            foreach (var entry in dataGridViewHostsEntries.SelectedHostEntries.ToList())
            {
                HostsFile.Instance.Entries.InsertAfter(entry, new HostsEntry(entry));
            }
        }
        else if (dataGridViewHostsEntries.CurrentRow?.DataBoundItem != null)
        {
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            if (currentEntry != null)
            {
                HostsFile.Instance.Entries.InsertAfter(
                    currentEntry,
                    new HostsEntry(currentEntry));
            }
        }
    }

    /// <summary>
    /// Occurs when disable hosts clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnDisableHostsClick(object sender, EventArgs e)
    {
        // Source of truth: the hosts file is disabled when the live hosts file is
        // absent (renamed to .disabled), rather than trusting the sender's checkbox.
        var currentlyDisabled = !HostsFile.IsEnabled;

        menuDisable.Checked = !currentlyDisabled;
        buttonDisable.Checked = !currentlyDisabled;
        menuContextDisable.Checked = !currentlyDisabled;

        if (currentlyDisabled)
        {
            HostsFile.EnableHostsFile();
        }
        else
        {
            HostsFile.DisableHostsFile();
        }

        UpdateNotifyIcon();
    }

    /// <summary>
    /// The on edit click.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnEditClick(object sender, EventArgs e) => this.ShowOrActivate();

    /// <summary>
    /// Occurs when filter comment clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnFilterCommentClick(object sender, EventArgs e)
    {
        var newState = !(_filter?.Comments ?? false);

        _filter?.Comments = newState;

        menuFilterComments.Checked = newState;
        buttonFilterComment.Checked = newState;

        _hostEntriesView?.Refresh();
    }

    /// <summary>
    /// Occurs when filter disabled clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnFilterDisabledClick(object sender, EventArgs e)
    {
        var newState = !(_filter?.Disabled ?? false);

        _filter?.Disabled = newState;

        menuFilterDisabled.Checked = newState;
        buttonFilterDisabled.Checked = newState;

        _hostEntriesView?.Refresh();
    }

    /// <summary>
    /// Occurs when filter text changed.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnFilterTextChanged(object sender, EventArgs e) => _hostEntriesView?.Refresh();

    /// <summary>
    /// Occurs when form loads.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnFomLoad(object sender, EventArgs e)
    {
        LoadSettings();

        _hostsArchiveView = new BindingListView<HostsArchive>(components)
        {
            DataSource = HostsArchiveList.Instance
        };

        bindingSourceArchive.DataSource = _hostsArchiveView;
        _hostsArchiveView.Sort = nameof(HostsArchive.FileName);

        bindingSourceHostFile.DataSource = HostsFile.Instance;

        _hostEntriesView = new BindingListView<HostsEntry>(components)
        {
            DataSource = HostsFile.Instance.Entries
        };

        _hostEntriesView.AddingNew += (s, args) => args.NewObject = new HostsEntry(string.Empty);

        // Tell grid how to clear sort of underlying data source
        // since it doesn't know how by itself
        dataGridViewHostsEntries.ClearSort = () =>
        {
            _hostEntriesView.RemoveSort();
        };

        bindingSourceView.DataSource = _hostEntriesView;

        _filter = new HostsFilter(
                hostEntry => hostEntry.ToString().Contains(textFilter.Text));

        _hostEntriesView.Filter = _filter;

        HostsFile.Instance.Entries.ResetBindings();

        menuDisable.Checked = !HostsFile.IsEnabled;
        buttonDisable.Checked = !HostsFile.IsEnabled;

        UpdateNotifyIcon();

        // HACK: Make sure a newly added row gets committed after
        // the first cell is validated so HostsEntry validation and data
        // binding behaves correctly
        _hostEntriesView.AddingNew +=
            (sender1, e1) =>
            {
                _addingNew = true;
            };

        dataGridViewHostsEntries.CellValidated +=
            (sender1, e1) =>
            {
                if (!_ignoreAddingNew && _addingNew)
                {
                    _hostEntriesView.EndNew(_hostEntriesView.Count - 1);
                    _addingNew = false;
                }
            };

        dataGridViewHostsEntries.CurrentCellChanged +=
            (sender1, e1) =>
            {
                _ignoreAddingNew = true;
            };

        dataGridViewHostsEntries.CurrentCellDirtyStateChanged +=
            (sender1, e1) =>
            {
                _ignoreAddingNew = false;
            };
    }


    /// <summary>
    /// Occurs when form is closing.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnFormClosing(object sender, FormClosingEventArgs e)
    {
        if (e.CloseReason != CloseReason.ApplicationExitCall)
        {
            e.Cancel = true;
            Hide();
        }
    }

    /// <summary>
    /// Occurs when form shown for first time.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnFormShown(object sender, EventArgs e)
    {
        dataGridViewHostsEntries.AutoResizeColumn(
            columnEnabled.Index,
            DataGridViewAutoSizeColumnMode.AllCells);

        dataGridViewHostsEntries.AutoResizeColumn(
            columnIpAddress.Index,
            DataGridViewAutoSizeColumnMode.AllCells);

        // Add room for error provider
        columnIpAddress.Width += 20;

        dataGridViewHostsEntries.AutoResizeColumn(
            columnHostnames.Index,
            DataGridViewAutoSizeColumnMode.AllCells);

        // HACK: calling focus causes cell validate to occur
        // which causes row to be committed
        _ignoreAddingNew = true;

        textFilter.Focus();

        // Deselect top left cell for aesthetics
        foreach (DataGridViewCell cell in dataGridViewHostsEntries.SelectedCells)
        {
            cell.Selected = false;
        }

        _ignoreAddingNew = false;
    }

    /// <summary>
    /// Occurs when import clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnImportClick(object sender, EventArgs e)
    {
        var result = openFileDialog.ShowDialog(this);

        if (result == DialogResult.OK)
        {
            HostsFile.Instance.Import(
                openFileDialog.FileName);
        }
    }

    /// <summary>
    /// Occurs when exit clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnExitClick(object sender, EventArgs e)
    {
        SaveSettings();
        Application.Exit();
    }

    /// <summary>
    /// Occurs when restore clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnRestoreClick(object sender, EventArgs e) => HostsFile.Instance.RestoreDefault();

    /// <summary>
    /// Occurs when save as clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnSaveAsClick(object sender, EventArgs e)
    {
        dataGridViewHostsEntries.CommitEdit(
            DataGridViewDataErrorContexts.Commit);

        var result = saveFileDialog.ShowDialog(this);

        if (result == DialogResult.OK)
        {
            HostsFile.Instance.SaveAs(saveFileDialog.FileName);
        }
    }

    /// <summary>
    /// Occurs when save clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnSaveClick(object sender, EventArgs e)
    {
        dataGridViewHostsEntries.CommitEdit(
            DataGridViewDataErrorContexts.Commit);

        HostsFile.Instance.Save();
    }

    /// <summary>
    /// Occurs when move down clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnMoveDownClick(object sender, EventArgs e)
    {
        if (dataGridViewHostsEntries.SelectedRows.Count > 0)
        {
            var selectedEntries = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            var maxRowIndex = dataGridViewHostsEntries.SelectedRows.Cast<DataGridViewRow>().Max(row => row.Index);
            var belowEntry = dataGridViewHostsEntries.GetHostEntry(maxRowIndex + 1);

            if (belowEntry != null)
            {
                HostsFile.Instance.Entries.MoveAfter(selectedEntries, belowEntry);

                dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
            }
        }
        else if (dataGridViewHostsEntries.CurrentHostEntry != null && dataGridViewHostsEntries.CurrentRow != null)
        {
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            var belowEntry = dataGridViewHostsEntries.GetHostEntry(dataGridViewHostsEntries.CurrentRow.Index + 1);

            if (belowEntry != null)
            {
                var selectedEntries = new List<HostsEntry>([currentEntry]);

                HostsFile.Instance.Entries.MoveAfter([currentEntry], belowEntry);

                dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
            }
        }
    }

    /// <summary>
    /// Occurs when move up clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnMoveUpClick(object sender, EventArgs e)
    {
        if (dataGridViewHostsEntries.SelectedRows.Count > 0)
        {
            var selectedEntries = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            var minRowIndex = dataGridViewHostsEntries.SelectedRows.Cast<DataGridViewRow>().Min(row => row.Index);
            var aboveEntry = dataGridViewHostsEntries.GetHostEntry(minRowIndex - 1);

            if (aboveEntry != null)
            {
                HostsFile.Instance.Entries.MoveBefore(selectedEntries, aboveEntry);

                dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
            }
        }
        else if (dataGridViewHostsEntries.CurrentHostEntry != null && dataGridViewHostsEntries.CurrentRow != null)
        {
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            var aboveEntry = dataGridViewHostsEntries.GetHostEntry(dataGridViewHostsEntries.CurrentRow.Index - 1);

            if (aboveEntry != null)
            {
                var selectedEntries = new List<HostsEntry>([currentEntry]);

                HostsFile.Instance.Entries.MoveBefore([currentEntry], aboveEntry);

                dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
            }
        }
    }

    /// <summary>
    /// The on notify icon double click.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnNotifyIconDoubleClick(object sender, EventArgs e) => this.ShowOrActivate();

    /// <summary>
    /// Occurs when paste clicked.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnPasteClick(object sender, EventArgs e)
    {
        // HACK: If editing cell forward cut/copy/paste command
        // to editing control
        if (dataGridViewHostsEntries.IsCurrentCellInEditMode)
        {
            var keys = menuPaste.ShortcutKeys;
            menuPaste.ShortcutKeys = Keys.None;
            menuContextPaste.ShortcutKeys = Keys.None;
            SendKeys.SendWait("^(V)");
            menuPaste.ShortcutKeys = keys;
            menuContextPaste.ShortcutKeys = keys;
            return;
        }

        dataGridViewHostsEntries.CancelEdit();

        if (dataGridViewHostsEntries.SelectedRows.Count > 0 &&
            _clipboardEntries != null)
        {
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            if (currentEntry != null)
            {
                HostsFile.Instance.Entries.Insert(currentEntry, _clipboardEntries);
            }

            _clipboardEntries = null;
        }
        else
        {
            foreach (
                DataGridViewCell cell in
                dataGridViewHostsEntries.SelectedCells)
            {
                if (cell.ValueType == typeof(string))
                {
                    cell.Value = Clipboard.GetText();
                }
            }
        }
    }

    /// <summary>
    /// Occurs when form's Visible property changed.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnVisibleChanged(object sender, EventArgs e) => ShowInTaskbar = Visible;

    /// <summary>
    /// Updates the notify icon.
    /// </summary>
    private void UpdateNotifyIcon()
    {
        notifyIcon.Icon =
            HostsFile.IsEnabled ?
            Resources.HostsFileEditor :
            Resources.HostsFileEditorDisabled;
    }

    /// <summary>
    /// Called when insert above clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/>
    /// instance containing the event data.</param>
    private void OnInsertAboveClick(object sender, EventArgs e)
    {
        if (dataGridViewHostsEntries.CurrentRow?.DataBoundItem != null)
        {
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            if (currentEntry != null)
            {
                HostsFile.Instance.Entries.InsertBefore(currentEntry);
            }
        }
        else
        {
            dataGridViewHostsEntries.CancelEdit();
            HostsFile.Instance.Entries.Add();
        }
    }

    /// <summary>
    /// Called when insert below clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance 
    /// containing the event data.</param>
    private void OnInsertBelowClick(object sender, EventArgs e)
    {
        if (dataGridViewHostsEntries.CurrentRow?.DataBoundItem != null)
        {
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            if (currentEntry != null)
            {
                HostsFile.Instance.Entries.InsertAfter(currentEntry);
            }
        }
        else
        {
            dataGridViewHostsEntries.CancelEdit();
            HostsFile.Instance.Entries.Add();
        }
    }

    /// <summary>
    /// Called when refresh clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnRefreshClick(object sender, EventArgs e)
    {
        var result = MessageBox.Show(
            this,
            Resources.LoseChangesQuestion,
            Resources.LoseChangesDialogCaption,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);

        if (result == DialogResult.Yes)
        {
            HostsFile.Instance.Refresh();
        }
    }

    /// <summary>
    /// Called when view archive clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnViewArchiveClick(object sender, EventArgs e)
    {
        // Source of truth: the archive panel's collapsed state.
        var archiveVisible = !splitContainer.Panel2Collapsed;

        menuViewArchive.Checked = !archiveVisible;
        buttonViewArchive.Checked = !archiveVisible;

        splitContainer.Panel2Collapsed = archiveVisible;
    }

    /// <summary>
    /// Called when undo clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance 
    /// containing the event data.</param>
    private void OnUndoClick(object sender, EventArgs e) => UndoManager.Instance.Undo();

    /// <summary>
    /// Called when redo clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance 
    /// containing the event data.</param>
    private void OnRedoClick(object sender, EventArgs e) => UndoManager.Instance.Redo();

    /// <summary>
    /// Called when archive delete clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance 
    /// containing the event data.</param>
    private void OnArchiveDeleteClick(object sender, EventArgs e)
    {
        var archive = dataGridViewArchive.CurrentHostsArchive;

        if (archive != null)
        {
            HostsArchiveList.Instance.Delete(archive);
        }
    }

    /// <summary>
    /// Called when archive load clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance
    /// containing the event data.</param>
    private void OnArchiveLoadClick(object sender, EventArgs e)
    {
        var archive = dataGridViewArchive.CurrentHostsArchive;

        if (archive != null)
        {
            HostsFile.Instance.Import(archive.FilePath);
        }
    }

    /// <summary>
    /// Called when ping IPs clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance
    /// containing the event data.</param>
    private void OnPingIPsClick(object sender, EventArgs e)
    {
        var newState = !HostsEntry.AutoPingIPAddress;

        HostsEntry.AutoPingIPAddress = newState;

        menuPingIPs.Checked = newState;
    }

    /// <summary>
    /// Called when remove default text clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> 
    /// instance containing the event data.</param>
    private void OnRemoveDefaultTextClick(object sender, EventArgs e)
    {
        var newState = !HostsFile.RemoveDefaultText;

        HostsFile.RemoveDefaultText = newState;
        menuRemoveDefaultText.Checked = newState;
    }

    /// <summary>
    /// Loads the settings.
    /// </summary>
    private void LoadSettings()
    {
        var settings = Settings.Default;

        HostsEntry.AutoPingIPAddress = settings.AutoPingIPAddresses;
        HostsFile.RemoveDefaultText = settings.RemoveDefaultText;

        menuPingIPs.Checked = HostsEntry.AutoPingIPAddress;
        menuRemoveDefaultText.Checked = HostsFile.RemoveDefaultText;
        menuViewArchive.Checked = settings.ArchiveVisible;
        buttonViewArchive.Checked = settings.ArchiveVisible;
        splitContainer.Panel2Collapsed = !settings.ArchiveVisible;

        // Restore saved bounds only if the window would actually be visible: require the
        // saved rectangle (not merely its top-left corner) to overlap a screen work area.
        var savedBounds = new Rectangle(settings.WindowLocation, settings.WindowSize);
        if (!settings.WindowSize.IsEmpty &&
            Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(savedBounds)))
        {
            Location = settings.WindowLocation;
            Size = settings.WindowSize;
        }

        splitContainer.SplitterDistance = settings.SplitterWidth;
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    private void SaveSettings()
    {
        var settings = Settings.Default;

        settings.AutoPingIPAddresses = HostsEntry.AutoPingIPAddress;
        settings.RemoveDefaultText = HostsFile.RemoveDefaultText;
        settings.WindowLocation = Location;
        settings.ArchiveVisible = menuViewArchive.Checked;

        // Save size for normal window, don't save anything for minimized
        // since that's probably not what the user wants next time
        // they open
        if (WindowState == FormWindowState.Normal)
        {
            settings.WindowSize = Size;
            settings.WindowState = WindowState;
        }
        else if (WindowState == FormWindowState.Maximized)
        {
            settings.WindowState = WindowState;
        }

        settings.SplitterWidth = splitContainer.SplitterDistance;

        settings.Save();
    }

    /// <summary>
    /// Called when resizing ends.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">
    /// The <see cref="System.EventArgs"/> instance containing the event 
    /// data.
    /// </param>
    private void OnResizingEnd(object sender, EventArgs e)
    {
        // Update settings to save last valid size 
        // (not in maximized or minimized mode)
        if (WindowState == FormWindowState.Normal)
        {
            Settings.Default.WindowSize = Size;
        }
    }

    /// <summary>
    /// Called when remove sort clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnRemoveSortClick(object sender, EventArgs e) => _hostEntriesView?.RemoveSort();

    /// <summary>
    /// Called when check clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnCheckClick(object sender, EventArgs e)
    {
        dataGridViewHostsEntries.CancelEdit();

        if (dataGridViewHostsEntries.SelectedRows.Count > 0)
        {
            HostsFile.Instance.Entries.SetEnabled(
                 dataGridViewHostsEntries.SelectedHostEntries,
                 isEnabled: true);
        }
        else if (dataGridViewHostsEntries.CurrentHostEntry != null)
        {
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            if (currentEntry != null)
            {
                HostsFile.Instance.Entries.SetEnabled(
                    [currentEntry],
                    isEnabled: true);
            }
        }
    }

    /// <summary>
    /// Called when uncheck clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnUncheckClick(object sender, EventArgs e)
    {
        dataGridViewHostsEntries.CancelEdit();

        if (dataGridViewHostsEntries.SelectedRows.Count > 0)
        {
            HostsFile.Instance.Entries.SetEnabled(
                 dataGridViewHostsEntries.SelectedHostEntries,
                 isEnabled: false);
        }
        else if (dataGridViewHostsEntries.CurrentHostEntry != null)
        {
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            if (currentEntry != null)
            {
                HostsFile.Instance.Entries.SetEnabled(
                    [currentEntry],
                    isEnabled: false);
            }
        }
    }

    /// <summary>
    /// Called when about clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnAboutClick(object sender, EventArgs e)
    {
        using var aboutForm = new AboutForm();
        aboutForm.ShowDialog(this);
    }

    /// <summary>
    /// Called when open text editor clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnOpenTextEditorClick(object sender, EventArgs e) => FileOpener.OpenTextFile(HostsFile.DefaultHostFilePath);
}
