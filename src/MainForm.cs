// <copyright file="MainForm.cs" company="N/A">
// Copyright 2025 Scott M. Lerch
// 
// This file is part of HostsFileEditor.
// 
// HostsFileEditor is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free 
// Software Foundation, either version 2 of the License, or (at your option)
// any later version.
// 
// HostsFileEditor is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
// 
// You should have received a copy of the GNU General Public   License along
// with HostsFileEditor. If not, see http://www.gnu.org/licenses/.
// </copyright>

using Equin.ApplicationFramework;
using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace HostsFileEditor;

/// <summary>
/// The main form for the application.
/// </summary>
internal partial class MainForm : Form
{
    /// <summary>
    /// The filter.
    /// </summary>
    private HostsFilter filter;

    /// <summary>
    /// The host entries view.
    /// </summary>
    private BindingListView<HostsEntry> hostEntriesView;

    /// <summary>
    /// The hosts archive view.
    /// </summary>
    private BindingListView<HostsArchive> hostsArchiveView;

    /// <summary>
    /// The clipboard host entries.
    /// </summary>
    private IEnumerable<HostsEntry> clipboardEntries;

    /// <summary>
    /// Determines if user is currently adding a new row.  Used for ugly
    /// hacks setup in load event.
    /// </summary>
    private bool addingNew;

    /// <summary>
    /// Ignore adding new in progress. Used for ugly hacks setup in load 
    /// event.
    /// </summary>
    private bool ignoreAddingNew;

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
        if (message.Msg == ProgramSingleInstance.WM_SHOWFIRSTINSTANCE)
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

        DialogResult result = inputDialog.ShowDialog(this);

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
            clipboardEntries = [.. dataGridViewHostsEntries.SelectedHostEntries.Select(entry => new HostsEntry(entry))];
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
                    builder.Append(cell.Value.ToString());
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
            clipboardEntries = [.. dataGridViewHostsEntries.SelectedHostEntries];

            HostsFile.Instance.Entries.Remove(clipboardEntries);
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
                    builder.Append(cell.Value.ToString());
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
            foreach (var entry in dataGridViewHostsEntries.SelectedHostEntries)
            {
                HostsFile.Instance.Entries.InsertAfter(entry, new HostsEntry(entry));
            }
        }
        else if (dataGridViewHostsEntries.CurrentRow.DataBoundItem != null)
        {
            HostsFile.Instance.Entries.InsertAfter(
                dataGridViewHostsEntries.CurrentHostEntry,
                new HostsEntry(dataGridViewHostsEntries.CurrentHostEntry));
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
        bool checkState = (sender as dynamic).Checked;

        menuDisable.Checked = !checkState;
        buttonDisable.Checked = !checkState;
        menuContextDisable.Checked = !checkState;

        if (checkState)
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
    private void OnEditClick(object sender, EventArgs e)
    {
        this.ShowOrActivate();
    }

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
        bool checkState = (sender as dynamic).Checked;

        filter.Comments = !checkState;

        menuFilterComments.Checked = !checkState;
        buttonFilterComment.Checked = !checkState;

        hostEntriesView.Refresh();
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
        bool checkState = (sender as dynamic).Checked;

        filter.Disabled = !checkState;

        menuFilterDisabled.Checked = !checkState;
        buttonFilterDisabled.Checked = !checkState;

        hostEntriesView.Refresh();
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
    private void OnFilterTextChanged(object sender, EventArgs e)
    {
        hostEntriesView.Refresh();
    }

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

        hostsArchiveView = new BindingListView<HostsArchive>(components)
        {
            DataSource = HostsArchiveList.Instance
        };

        bindingSourceArchive.DataSource = hostsArchiveView;
        hostsArchiveView.Sort = Reflect.GetPropertyName(() => new HostsArchive().FileName);

        bindingSourceHostFile.DataSource = HostsFile.Instance;

        hostEntriesView = new BindingListView<HostsEntry>(components)
        {
            DataSource = HostsFile.Instance.Entries
        };

        hostEntriesView.AddingNew += (s, args) => args.NewObject = new HostsEntry(string.Empty);

        // Tell grid how to clear sort of underlying data source
        // since it doesn't know how by itself
        dataGridViewHostsEntries.ClearSort = () =>
        {
            hostEntriesView.RemoveSort();
        };

        bindingSourceView.DataSource = hostEntriesView;

        filter = new HostsFilter(
                hostEntry => hostEntry.ToString().Contains(textFilter.Text));

        hostEntriesView.Filter = filter;

        HostsFile.Instance.Entries.ResetBindings();

        menuDisable.Checked = !HostsFile.IsEnabled;
        buttonDisable.Checked = !HostsFile.IsEnabled;

        UpdateNotifyIcon();

        // HACK: Make sure a newly added row gets committed after
        // the first cell is validated so HostsEntry validation and data
        // binding behaves correctly
        hostEntriesView.AddingNew +=
            (sender1, e1) =>
            {
                addingNew = true;
            };

        dataGridViewHostsEntries.CellValidated +=
            (sender1, e1) =>
            {
                if (!ignoreAddingNew && addingNew)
                {
                    hostEntriesView.EndNew(hostEntriesView.Count - 1);
                    addingNew = false;
                }
            };

        dataGridViewHostsEntries.CurrentCellChanged +=
            (sender1, e1) =>
            {
                ignoreAddingNew = true;
            };

        dataGridViewHostsEntries.CurrentCellDirtyStateChanged +=
            (sender1, e1) =>
            {
                ignoreAddingNew = false;
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
        ignoreAddingNew = true;

        textFilter.Focus();

        // Deselect top left cell for aesthetics
        foreach (DataGridViewCell cell in dataGridViewHostsEntries.SelectedCells)
        {
            cell.Selected = false;
        }

        ignoreAddingNew = false;
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
        DialogResult result = openFileDialog.ShowDialog(this);

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
    private void OnRestoreClick(object sender, EventArgs e)
    {
        HostsFile.Instance.RestoreDefault();
    }

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

        DialogResult result = saveFileDialog.ShowDialog(this);

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

            HostsFile.Instance.Entries.MoveAfter(
                dataGridViewHostsEntries.SelectedHostEntries,
                dataGridViewHostsEntries.FirstSelectedHostEntry);

            dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
        }
        else if (dataGridViewHostsEntries.CurrentHostEntry != null)
        {
            var selectedEntries = new List<HostsEntry>(
                [dataGridViewHostsEntries.CurrentHostEntry]);

            HostsFile.Instance.Entries.MoveAfter(
                [dataGridViewHostsEntries.CurrentHostEntry],
                dataGridViewHostsEntries.CurrentHostEntry);

            dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
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

            HostsFile.Instance.Entries.MoveBefore(
                 dataGridViewHostsEntries.SelectedHostEntries,
                 dataGridViewHostsEntries.LastSelectedHostEntry);

            dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
        }
        else if (dataGridViewHostsEntries.CurrentHostEntry != null)
        {
            var selectedEntries = new List<HostsEntry>(
                [dataGridViewHostsEntries.CurrentHostEntry]);

            HostsFile.Instance.Entries.MoveBefore(
                [dataGridViewHostsEntries.CurrentHostEntry],
                dataGridViewHostsEntries.CurrentHostEntry);

            dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
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
    private void OnNotifyIconDoubleClick(object sender, EventArgs e)
    {
        this.ShowOrActivate();
    }

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
            clipboardEntries != null)
        {
            HostsFile.Instance.Entries.Insert(
                dataGridViewHostsEntries.CurrentHostEntry, 
                clipboardEntries);

            clipboardEntries = null;
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
    private void OnVisibleChanged(object sender, EventArgs e)
    {
        ShowInTaskbar = Visible;
    }

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
        if (dataGridViewHostsEntries.CurrentRow.DataBoundItem != null)
        {
            HostsFile.Instance.Entries.InsertBefore(
                dataGridViewHostsEntries.CurrentHostEntry);
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
        if (dataGridViewHostsEntries.CurrentRow.DataBoundItem != null)
        {
            HostsFile.Instance.Entries.InsertAfter(
                dataGridViewHostsEntries.CurrentHostEntry);
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
        DialogResult result = MessageBox.Show(
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
        bool isChecked = ((dynamic)sender).Checked;

        menuViewArchive.Checked = !isChecked;
        buttonViewArchive.Checked = !isChecked;

        splitContainer.Panel2Collapsed = isChecked;
    }

    /// <summary>
    /// Called when undo clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance 
    /// containing the event data.</param>
    private void OnUndoClick(object sender, EventArgs e)
    {
        UndoManager.Instance.Undo();
    }

    /// <summary>
    /// Called when redo clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance 
    /// containing the event data.</param>
    private void OnRedoClick(object sender, EventArgs e)
    {
        UndoManager.Instance.Redo();
    }

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
        bool isChecked = (sender as dynamic).Checked;

        HostsEntry.AutoPingIPAddress = !isChecked;

        menuPingIPs.Checked = !isChecked;
    }

    /// <summary>
    /// Called when remove default text clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> 
    /// instance containing the event data.</param>
    private void OnRemoveDefaultTextClick(object sender, EventArgs e)
    {
        menuRemoveDefaultText.Checked = 
            !menuRemoveDefaultText.Checked;

        HostsFile.RemoveDefaultText = 
            menuRemoveDefaultText.Checked;
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

        // Do quick check that saved location still exists for multi-monitor setups
        if (Screen.AllScreens.Any(screen =>
            screen.WorkingArea.Contains(settings.WindowLocation)))
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
        Settings settings = Settings.Default;

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
    private void OnRemoveSortClick(object sender, EventArgs e)
    {
        hostEntriesView.RemoveSort();
    }

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
            HostsFile.Instance.Entries.SetEnabled(
                [dataGridViewHostsEntries.CurrentHostEntry],
                isEnabled: true);
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
            HostsFile.Instance.Entries.SetEnabled(
                [dataGridViewHostsEntries.CurrentHostEntry],
                isEnabled: false);
        }
    }

    /// <summary>
    /// Called when about clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnAboutClick(object sender, EventArgs e)
    {
        (new AboutForm()).ShowDialog(this);
    }

    /// <summary>
    /// Called when open text editor clicked.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void OnOpenTextEditorClick(object sender, EventArgs e)
    {
        FileOpener.OpenTextFile(HostsFile.DefaultHostFilePath);
    }
}