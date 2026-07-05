using Equin.ApplicationFramework;
using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.Configuration;
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

        // The ToolStripContainer's top panel doesn't reliably order the two strips from the
        // designer (Dock=None + Location isn't honored for row order, so the menu ends up below
        // the toolbar). Pin the rows explicitly: menu on top (row 0), toolbar beneath (row 1).
        var topPanel = toolStripContainer.TopToolStripPanel;
        topPanel.SuspendLayout();
        topPanel.Controls.Remove(menuStrip);
        topPanel.Controls.Remove(toolStrip);
        topPanel.Join(menuStrip, 0);
        topPanel.Join(toolStrip, 1);
        topPanel.ResumeLayout(true);

        // Must be a directory: setting InitialDirectory to the hosts *file* path made Windows
        // ignore it, so Save As opened in the default location instead of the etc directory.
        saveFileDialog.InitialDirectory = HostsFile.DefaultHostFileDirectory;

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

    /// <inheritdoc />
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Menu/context-menu shortcut keys (Del, Ctrl+X/C/V, Ctrl+D, Alt+Up/Down, etc.) are
        // dispatched form-wide before the focused control sees them. When the user is typing in
        // the filter textbox, let it handle these as normal text editing instead of acting on
        // the grid — otherwise, e.g., Del in the filter box deletes the selected hosts rows.
        //
        // The grid-focus guard is essential: whenever the grid (or its active cell editor) has
        // focus, these shortcuts MUST reach the menu handlers so copy/cut/paste/delete of rows
        // keep working. Only suppress when the grid has no focus and the filter box does.
        if (IsGridShortcut(keyData)
            && !dataGridViewHostsEntries.ContainsFocus
            && textFilter.Focused)
        {
            return false;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static bool IsGridShortcut(Keys keyData) => keyData switch
    {
        Keys.Delete => true,
        Keys.Control | Keys.C => true,
        Keys.Control | Keys.X => true,
        Keys.Control | Keys.V => true,
        Keys.Control | Keys.A => true,
        Keys.Control | Keys.Z => true,
        Keys.Control | Keys.D => true,
        Keys.Alt | Keys.Up => true,
        Keys.Alt | Keys.Down => true,
        _ => false,
    };

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
    /// Sets the system clipboard text, skipping the call for empty content
    /// (Clipboard.SetText throws ArgumentNullException on null or empty).
    /// </summary>
    private static void SetClipboardTextSafe(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
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
        // HACK: forward Ctrl+C/X/V to the in-cell text editor ONLY when editing a cell's text
        // with no full row selected. This grid keeps the current cell in edit mode, so without
        // the row guard the hack fired on row selections too and copy/cut/paste never reached
        // the row logic below (Delete worked only because it has no such check).
        if (dataGridViewHostsEntries.IsCurrentCellInEditMode
            && dataGridViewHostsEntries.SelectedRowCount == 0)
        {
            var keys = menuCopy.ShortcutKeys;
            menuCopy.ShortcutKeys = Keys.None;
            menuContextCopy.ShortcutKeys = Keys.None;
            SendKeys.SendWait("^(C)");
            menuCopy.ShortcutKeys = keys;
            menuContextCopy.ShortcutKeys = keys;
            return;
        }

        if (dataGridViewHostsEntries.SelectedRowCount > 0)
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

            SetClipboardTextSafe(builder.ToString());
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
        // HACK: forward Ctrl+C/X/V to the in-cell text editor ONLY when editing a cell's text
        // with no full row selected. This grid keeps the current cell in edit mode, so without
        // the row guard the hack fired on row selections too and copy/cut/paste never reached
        // the row logic below (Delete worked only because it has no such check).
        if (dataGridViewHostsEntries.IsCurrentCellInEditMode
            && dataGridViewHostsEntries.SelectedRowCount == 0)
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

        if (dataGridViewHostsEntries.SelectedRowCount > 0)
        {
            // Snapshot the actual selected entries so we remove THOSE, then clone independent
            // copies for the clipboard. Removing the clones (as before) matched nothing by
            // reference, so Cut silently behaved like Copy.
            var selected = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            _clipboardEntries = [.. selected.Select(entry => new HostsEntry(entry))];

            // Clear the selection before removing — see OnDeleteClick: removing selected rows makes
            // the DataGridView reconcile a huge selection O(n^2) afterwards (a multi-minute hang).
            dataGridViewHostsEntries.ClearSelection();
            HostsFile.Instance.Entries.Remove(selected);
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

            SetClipboardTextSafe(builder.ToString());
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
        // HACK: forward Delete to the in-cell text editor when editing a cell's text with no
        // full row selected, so it removes the character in front of the cursor instead of
        // wiping the cell (issue #36). Mirrors the Ctrl+C/X/V forwarding in OnCopy/Cut/PasteClick;
        // both menuDelete and menuContextDelete carry the Del shortcut, so clear both to avoid
        // re-triggering this handler while the synthetic keystroke is dispatched.
        if (dataGridViewHostsEntries.IsCurrentCellInEditMode
            && dataGridViewHostsEntries.SelectedRowCount == 0)
        {
            var keys = menuDelete.ShortcutKeys;
            menuDelete.ShortcutKeys = Keys.None;
            menuContextDelete.ShortcutKeys = Keys.None;
            SendKeys.SendWait("{DEL}");
            menuDelete.ShortcutKeys = keys;
            menuContextDelete.ShortcutKeys = keys;
            return;
        }

        if (dataGridViewHostsEntries.SelectedRowCount > 0)
        {
            // Snapshot the selection, then CLEAR it before removing the rows. Removing rows while
            // they are still selected makes the DataGridView reconcile the (huge) selection
            // row-by-row in a posted operation that is O(n^2) — it froze the UI for over a minute on
            // a 400K-row file even though OnDeleteClick itself returned in ~30ms. Clearing first is
            // O(n) and the rows are being deleted anyway.
            var selected = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            dataGridViewHostsEntries.ClearSelection();
            HostsFile.Instance.Entries.Remove(selected);
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
        if (dataGridViewHostsEntries.SelectedRowCount > 0)
        {
            // Snapshot the selection, then clear it before the bulk duplicate: Duplicate raises a
            // single Reset, and reconciling a huge selection against that Reset is O(n^2) in the grid
            // (the same teardown that froze Select-All + Delete). Duplicate inserts a copy after each
            // original in one O(n) rebind — a per-row InsertAfter loop would be O(n^2) at 400K.
            var sel = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            dataGridViewHostsEntries.ClearSelection();
            HostsFile.Instance.Entries.Duplicate(sel);
        }
        else if (dataGridViewHostsEntries.CurrentHostEntry is { } currentEntry)
        {
            HostsFile.Instance.Entries.Duplicate([currentEntry]);
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

        try
        {
            if (currentlyDisabled)
            {
                HostsFile.Instance.EnableHostsFile();
            }
            else
            {
                HostsFile.Instance.DisableHostsFile();
            }
        }
        catch (Elevation.ElevationCancelledException)
        {
            // User declined the UAC prompt; the file was not renamed.
        }
        finally
        {
            // Sync the toggles from the ACTUAL file state (not the intended one) so a declined
            // or failed elevation can't leave the menus/toolbar asserting a change that never
            // happened. Previously the checkboxes were set before the privileged op.
            var disabledNow = !HostsFile.IsEnabled;
            menuDisable.Checked = disabledNow;
            buttonDisable.Checked = disabledNow;
            menuContextDisable.Checked = disabledNow;
            UpdateNotifyIcon();
        }
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
    private async void OnFomLoad(object sender, EventArgs e)
    {
        LoadSettings();

        _hostsArchiveView = new BindingListView<HostsArchive>(components)
        {
            DataSource = HostsArchiveList.Instance
        };

        bindingSourceArchive.DataSource = _hostsArchiveView;
        _hostsArchiveView.Sort = nameof(HostsArchive.FileName);

        // Parse the hosts file off the UI thread (it can be very large) behind a loading indicator,
        // so the window paints and stays responsive instead of freezing on a huge file.
        UseWaitCursor = true;
        // Held separately so it can be disposed below: Control.Dispose does NOT dispose a Font the
        // caller assigned via the Font property, so `new Font(...)` inline would leak a GDI handle.
        var loadingFont = new Font(Font.FontFamily, 12f, FontStyle.Italic);
        var loadingLabel = new Label
        {
            Text = "Loading hosts file…",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = SystemColors.Window,
            Font = loadingFont,
            ForeColor = SystemColors.GrayText,
        };
        dataGridViewHostsEntries.Parent!.Controls.Add(loadingLabel);
        loadingLabel.BringToFront();

        try
        {
            await HostsFile.PreloadAsync();
        }
        catch (Exception ex)
        {
            // OnFomLoad is `async void`, so an exception from the off-thread load (locked/denied
            // hosts file, failed backup copy, bad HFE_HOSTS_PATH target) would otherwise escape
            // unobserved and crash. Surface it and leave an empty grid instead.
            MessageBox.Show(
                this,
                "The hosts file could not be loaded:" + Environment.NewLine + Environment.NewLine + ex.Message,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }
        finally
        {
            loadingLabel.Parent?.Controls.Remove(loadingLabel);
            loadingLabel.Dispose();
            loadingFont.Dispose();
            UseWaitCursor = false;
        }

        bindingSourceHostFile.DataSource = HostsFile.Instance;

        // Make a dev/test HFE_HOSTS_PATH override visible in the title bar so it's never a silent
        // redirect of the (privileged) hosts operations to an alternate file.
        if (HostsFile.OverridePath is { } overridePath)
        {
            Text += $" — [{overridePath}]";
        }

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
            UpdateMoveControlsEnabled();
        };

        // Move Up/Down reorders the underlying file, which is meaningless (and silently
        // destructive to resolution precedence) while a column sort is applied. Disable those
        // controls whenever a sort is active.
        dataGridViewHostsEntries.Sorted += (s, e) => UpdateMoveControlsEnabled();

        bindingSourceView.DataSource = _hostEntriesView;

        _filter = new HostsFilter(
                hostEntry => hostEntry.ToString().Contains(textFilter.Text));

        _hostEntriesView.Filter = _filter;

        HostsFile.Instance.Entries.ResetBindings();

        menuDisable.Checked = !HostsFile.IsEnabled;
        buttonDisable.Checked = !HostsFile.IsEnabled;
        menuContextDisable.Checked = !HostsFile.IsEnabled;

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
        // Minimize to tray only when the user closes the window. Never veto an OS-initiated
        // close (WindowsShutDown / logoff) — cancelling that blocks Windows shutdown — nor an
        // explicit Application.Exit.
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        // On OS shutdown/logoff the app exits without going through Exit, so persist settings
        // here too (Exit also saves for the ApplicationExitCall path; a redundant save is
        // harmless).
        SaveSettings();
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
        // File > Exit / tray Exit is the true-exit path (closing the window only hides to tray),
        // so warn about unsaved changes here before actually exiting.
        if (!ConfirmDiscardUnsavedChanges())
        {
            return;
        }

        SaveSettings();
        Application.Exit();
    }

    /// <summary>
    /// If there are unsaved hosts-file changes, prompts to save/discard/cancel. Returns true if
    /// the caller should proceed with exiting (saved or discarded), false to abort the exit.
    /// </summary>
    private bool ConfirmDiscardUnsavedChanges()
    {
        if (!HostsFile.Instance.IsModified)
        {
            return true;
        }

        var result = MessageBox.Show(
            this,
            "You have unsaved changes to the hosts file. Save them before exiting?",
            "Unsaved Changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        switch (result)
        {
            case DialogResult.Yes:
                try
                {
                    HostsFile.Instance.Save();
                }
                catch (Elevation.ElevationCancelledException)
                {
                    // Declined the elevation prompt; abort the exit so changes aren't lost.
                    return false;
                }

                return true;

            case DialogResult.No:
                return true;

            default:
                return false;
        }
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
        if (dataGridViewHostsEntries.SelectedRowCount > 0)
        {
            var selectedEntries = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            // GetLastRow is an O(n) scan; SelectedRows.Cast().Max(...) rebuilds the O(n^2) collection.
            var maxRowIndex = dataGridViewHostsEntries.Rows.GetLastRow(DataGridViewElementStates.Selected);
            // -1 means no selected row (e.g. selection cleared by a reentrant refresh after the guard).
            var belowEntry = maxRowIndex < 0 ? null : dataGridViewHostsEntries.GetHostEntry(maxRowIndex + 1);

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
        if (dataGridViewHostsEntries.SelectedRowCount > 0)
        {
            var selectedEntries = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            // GetFirstRow is an O(n) scan; SelectedRows.Cast().Min(...) rebuilds the O(n^2) collection.
            var minRowIndex = dataGridViewHostsEntries.Rows.GetFirstRow(DataGridViewElementStates.Selected);
            // -1 means no selected row (e.g. selection cleared by a reentrant refresh after the guard).
            var aboveEntry = minRowIndex < 0 ? null : dataGridViewHostsEntries.GetHostEntry(minRowIndex - 1);

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
        // HACK: forward Ctrl+C/X/V to the in-cell text editor ONLY when editing a cell's text with
        // no full row selected. This grid keeps the current cell in edit mode, so without the row
        // guard the hack fired on row selections too and copy/cut/paste never reached the row logic
        // below (Delete worked only because it has no such check). We must NOT also gate this on
        // `_clipboardEntries == null`: that clipboard lingers after any row Copy/Cut, so gating on it
        // would hijack in-cell text paste (pasting the stale copied row instead of the cell text) for
        // the rest of the session. After Cut-All the grid is empty and NOT in edit mode, so this guard
        // is false and the row-paste branch below still handles the paste-into-empty case.
        if (dataGridViewHostsEntries.IsCurrentCellInEditMode
            && dataGridViewHostsEntries.SelectedRowCount == 0)
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

        if (_clipboardEntries != null)
        {
            // Paste cut/copied rows relative to the current row. After Cut-All the list is empty,
            // so there is no current row / selection — append the clipboard to the end instead.
            var currentEntry = dataGridViewHostsEntries.CurrentHostEntry;
            if (currentEntry != null)
            {
                HostsFile.Instance.Entries.Insert(currentEntry, _clipboardEntries);
            }
            else
            {
                HostsFile.Instance.Entries.InsertRange(_clipboardEntries);
            }

            _clipboardEntries = null;
        }
        else if (dataGridViewHostsEntries.SelectedRowCount == 0)
        {
            // Cell-level text paste, only when no full rows are selected and there is no row
            // clipboard. Previously this ran whenever there were no internal clipboard entries —
            // including with rows selected (e.g. a second Ctrl+V after a row paste), which
            // overwrote every cell of the selected rows with the system clipboard text.
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
            // Honor the "remove default text" setting on reload, matching initial load and
            // Import; passing nothing would always strip the default hosts header.
            HostsFile.Instance.Refresh(HostsFile.RemoveDefaultText);
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
        Settings settings;
        try
        {
            settings = Settings.Default;

            // The first property access loads user.config; a corrupt file throws here. Recover
            // and start with defaults rather than failing to launch (issue #37).
            _ = settings.AutoPingIPAddresses;
        }
        catch (ConfigurationErrorsException ex)
        {
            ResetCorruptUserConfig(ex);
            return;
        }

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

        // Restore a maximized window (the Size/Location above become the normal bounds it uses
        // when un-maximized). SaveSettings persists this but it was never read back. Minimized
        // is intentionally not restored.
        if (settings.WindowState == FormWindowState.Maximized)
        {
            WindowState = FormWindowState.Maximized;
        }
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    private void SaveSettings()
    {
        try
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
        catch (ConfigurationErrorsException)
        {
            // Persisting settings must never crash the app (e.g. a corrupt or locked user.config
            // on exit/shutdown). Losing window bounds/preferences is acceptable; failing to close
            // is not (issue #37).
        }
    }

    /// <summary>
    /// Recovers from a corrupt user settings file by deleting it and reloading defaults, so a bad
    /// <c>user.config</c> cannot prevent the app from starting (issue #37).
    /// </summary>
    private static void ResetCorruptUserConfig(ConfigurationErrorsException ex)
    {
        // The offending path is on the exception (sometimes only on the inner one).
        var badFile = (ex.InnerException as ConfigurationErrorsException)?.Filename;
        if (string.IsNullOrEmpty(badFile))
        {
            badFile = ex.Filename;
        }

        try
        {
            if (!string.IsNullOrEmpty(badFile) && File.Exists(badFile))
            {
                File.Delete(badFile);
            }

            // Drop the faulted in-memory state so the rest of the session reads clean defaults.
            Settings.Default.Reload();
        }
        catch (IOException)
        {
            // Best effort — fall back to in-memory defaults for this session.
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (ConfigurationErrorsException)
        {
        }
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
    private void OnRemoveSortClick(object sender, EventArgs e)
    {
        _hostEntriesView?.RemoveSort();
        UpdateMoveControlsEnabled();
    }

    /// <summary>
    /// Enables the Move Up/Down commands only when no column sort is active. Moving reorders
    /// the underlying hosts file, which the sorted view would silently hide (the grid re-sorts,
    /// so the buttons appear dead while the real file order changes).
    /// </summary>
    private void UpdateMoveControlsEnabled()
    {
        var canMove = dataGridViewHostsEntries.SortOrder == SortOrder.None;
        menuMoveUp.Enabled = canMove;
        menuMoveDown.Enabled = canMove;
        menuContextMoveUp.Enabled = canMove;
        menuContextMoveDown.Enabled = canMove;
    }

    /// <summary>
    /// Called when check clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnCheckClick(object sender, EventArgs e)
    {
        dataGridViewHostsEntries.CancelEdit();

        if (dataGridViewHostsEntries.SelectedRowCount > 0)
        {
            // Snapshot then clear the selection before toggling: SetEnabled raises one Reset, and
            // reconciling a huge selection against it is the same O(n^2) grid teardown that froze
            // Select-All + Delete. (SetEnabled itself is now O(n) — it toggles without per-item events.)
            var sel = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            dataGridViewHostsEntries.ClearSelection();
            HostsFile.Instance.Entries.SetEnabled(sel, isEnabled: true);
        }
        else if (dataGridViewHostsEntries.CurrentHostEntry is { } currentEntry)
        {
            HostsFile.Instance.Entries.SetEnabled([currentEntry], isEnabled: true);
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

        if (dataGridViewHostsEntries.SelectedRowCount > 0)
        {
            // See OnCheckClick: snapshot then clear the selection before SetEnabled to avoid the
            // O(n^2) selection teardown against the single Reset on a huge hosts file.
            var sel = dataGridViewHostsEntries.SelectedHostEntries.ToList();
            dataGridViewHostsEntries.ClearSelection();
            HostsFile.Instance.Entries.SetEnabled(sel, isEnabled: false);
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
