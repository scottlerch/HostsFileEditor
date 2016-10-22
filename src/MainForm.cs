// <copyright file="MainForm.cs" company="N/A">
// Copyright 2011 Scott M. Lerch
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

namespace HostsFileEditor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Windows.Forms;
    using Equin.ApplicationFramework;
    using HostsFileEditor.Extensions;
    using HostsFileEditor.Properties;
    using HostsFileEditor.Utilities;

    /// <summary>
    /// The main form for the application.
    /// </summary>
    internal partial class MainForm : Form
    {
        #region Constants and Fields

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

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm"/> class.
        /// </summary>
        public MainForm()
        {
            this.InitializeComponent();

            this.saveFileDialog.InitialDirectory = HostsFile.DefaultHostFilePath;

            // Prevent data binding from setting properties to null when
            // an empty string is typed in
            this.columnComment.DefaultCellStyle.NullValue = null;
            this.columnIpAddress.DefaultCellStyle.NullValue = null;
            this.columnHostnames.DefaultCellStyle.NullValue = null;
        }

        #endregion

        #region Methods

        /// <summary>
        /// The window procedure.
        /// </summary>
        /// <param name="message">The message.</param>
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == ProgramSingleInstance.WM_SHOWFIRSTINSTANCE)
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
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
            this.dataGridViewHostsEntries.CommitEdit(
                DataGridViewDataErrorContexts.Commit);

            using (var inputDialog = new InputForm())
            {
                inputDialog.Text = this.Text;
                inputDialog.Prompt = Resources.InputArchivePrompt;

                DialogResult result = inputDialog.ShowDialog(this);

                if (result == DialogResult.OK)
                {
                    HostsFile.Instance.Archive(inputDialog.Input);
                }
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
            if (this.dataGridViewHostsEntries.IsCurrentCellInEditMode)
            {
                var keys = this.menuCopy.ShortcutKeys;
                this.menuCopy.ShortcutKeys = Keys.None;
                this.menuContextCopy.ShortcutKeys = Keys.None;
                SendKeys.SendWait("^(C)");
                this.menuCopy.ShortcutKeys = keys;
                this.menuContextCopy.ShortcutKeys = keys;
                return;
            }

            if (this.dataGridViewHostsEntries.SelectedRows.Count > 0)
            {
                this.clipboardEntries = this.dataGridViewHostsEntries
                    .SelectedHostEntries
                    .Select(entry => new HostsEntry(entry)).ToList();
            }
            else
            {
                StringBuilder builder = new StringBuilder();

                foreach (
                    DataGridViewCell cell in 
                    this.dataGridViewHostsEntries.SelectedCells)
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
            if (this.dataGridViewHostsEntries.IsCurrentCellInEditMode)
            {
                var keys = this.menuCut.ShortcutKeys;
                this.menuCut.ShortcutKeys = Keys.None;
                this.menuContextCut.ShortcutKeys = Keys.None;
                SendKeys.SendWait("^(X)");
                this.menuCut.ShortcutKeys = keys;
                this.menuContextCut.ShortcutKeys = keys;
                return;
            }

            this.dataGridViewHostsEntries.CancelEdit();

            if (this.dataGridViewHostsEntries.SelectedRows.Count > 0)
            {
                this.clipboardEntries = this.dataGridViewHostsEntries
                    .SelectedHostEntries
                    .ToList();

                HostsFile.Instance.Entries.Remove(this.clipboardEntries);
            }
            else
            {
                StringBuilder builder = new StringBuilder();

                foreach (
                    DataGridViewCell cell in 
                    this.dataGridViewHostsEntries.SelectedCells)
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
            if (this.dataGridViewHostsEntries.SelectedRows.Count > 0)
            {
                HostsFile.Instance.Entries.Remove(
                    this.dataGridViewHostsEntries.SelectedHostEntries);
            }
            else
            {
                foreach (
                    DataGridViewCell cell in 
                    this.dataGridViewHostsEntries.SelectedCells)
                {
                    if (cell.ValueType == typeof(string))
                    {
                        cell.Value = string.Empty;
                    }
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
            bool checkState = (sender as dynamic).Checked;

            this.menuDisable.Checked = !checkState;
            this.buttonDisable.Checked = !checkState;
            this.menuContextDisable.Checked = !checkState;

            if (checkState)
            {
                HostsFile.EnableHostsFile();
            }
            else
            {
                HostsFile.DisableHostsFile();
            }

            this.UpdateNotifyIcon();
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

            this.filter.Comments = !checkState;

            this.menuFilterComments.Checked = !checkState;
            this.buttonFilterComment.Checked = !checkState;

            this.hostEntriesView.Refresh();
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

            this.filter.Disabled = !checkState;

            this.menuFilterDisabled.Checked = !checkState;
            this.buttonFilterDisabled.Checked = !checkState;

            this.hostEntriesView.Refresh();
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
            this.hostEntriesView.Refresh();
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
            this.LoadSettings();

            this.hostsArchiveView = new BindingListView<HostsArchive>(this.components);
            this.hostsArchiveView.DataSource = HostsArchiveList.Instance;
            this.bindingSourceArchive.DataSource = this.hostsArchiveView;
            this.hostsArchiveView.Sort = Reflect.GetPropertyName(() => (new HostsArchive()).FileName);

            this.bindingSourceHostFile.DataSource = HostsFile.Instance;

            this.hostEntriesView = new BindingListView<HostsEntry>(this.components);
            this.hostEntriesView.DataSource = HostsFile.Instance.Entries;
            this.hostEntriesView.AddingNew += (s, args) => args.NewObject = new HostsEntry(string.Empty);

            // Tell grid how to clear sort of underlying data source
            // since it doesn't know how by itself
            this.dataGridViewHostsEntries.ClearSort = () =>
            {
                this.hostEntriesView.RemoveSort();
            };

            this.bindingSourceView.DataSource = this.hostEntriesView;

            this.filter = new HostsFilter(
                    hostEntry => hostEntry.ToString().Contains(this.textFilter.Text) ? true : false);

            this.hostEntriesView.Filter = this.filter;

            HostsFile.Instance.Entries.ResetBindings();

            this.menuDisable.Checked = !HostsFile.IsEnabled;
            this.buttonDisable.Checked = !HostsFile.IsEnabled;

            this.UpdateNotifyIcon();

            // HACK: Make sure a newly added row gets committed after
            // the first cell is validated so HostsEntry validation and data
            // binding behaves correctly
            this.hostEntriesView.AddingNew +=
                (sender1, e1) =>
                {
                    this.addingNew = true;
                };

            this.dataGridViewHostsEntries.CellValidated +=
                (sender1, e1) =>
                {
                    if (!this.ignoreAddingNew && this.addingNew)
                    {
                        this.hostEntriesView.EndNew(this.hostEntriesView.Count - 1);
                        this.addingNew = false;
                    }
                };

            this.dataGridViewHostsEntries.CurrentCellChanged +=
                (sender1, e1) =>
                {
                    this.ignoreAddingNew = true;
                };

            this.dataGridViewHostsEntries.CurrentCellDirtyStateChanged +=
                (sender1, e1) =>
                {
                    this.ignoreAddingNew = false;
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
                this.Hide();
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
            this.dataGridViewHostsEntries.AutoResizeColumn(
                this.columnEnabled.Index,
                DataGridViewAutoSizeColumnMode.AllCells);

            this.dataGridViewHostsEntries.AutoResizeColumn(
                this.columnIpAddress.Index,
                DataGridViewAutoSizeColumnMode.AllCells);

            // Add room for error provider
            this.columnIpAddress.Width += 20;

            this.dataGridViewHostsEntries.AutoResizeColumn(
                this.columnHostnames.Index,
                DataGridViewAutoSizeColumnMode.AllCells);

            // HACK: calling focus causes cell validate to occur
            // which causes row to be committed
            this.ignoreAddingNew = true;

            this.textFilter.Focus();

            // Deselect top left cell for aesthetics
            foreach (DataGridViewCell cell in this.dataGridViewHostsEntries.SelectedCells)
            {
                cell.Selected = false;
            }

            this.ignoreAddingNew = false;
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
            DialogResult result = this.openFileDialog.ShowDialog(this);

            if (result == DialogResult.OK)
            {
                HostsFile.Instance.Import(
                    this.openFileDialog.FileName);
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
            this.SaveSettings();
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
            this.dataGridViewHostsEntries.CommitEdit(
                DataGridViewDataErrorContexts.Commit);

            DialogResult result = this.saveFileDialog.ShowDialog(this);

            if (result == DialogResult.OK)
            {
                HostsFile.Instance.SaveAs(this.saveFileDialog.FileName);
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
            this.dataGridViewHostsEntries.CommitEdit(
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
            if (this.dataGridViewHostsEntries.SelectedRows.Count > 0)
            {
                var selectedEntries = this.dataGridViewHostsEntries.SelectedHostEntries.ToList();

                HostsFile.Instance.Entries.MoveAfter(
                    this.dataGridViewHostsEntries.SelectedHostEntries,
                    this.dataGridViewHostsEntries.FirstSelectedHostEntry);

                this.dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
            }
            else if (this.dataGridViewHostsEntries.CurrentHostEntry != null)
            {
                var selectedEntries = new List<HostsEntry>(
                    new[] { this.dataGridViewHostsEntries.CurrentHostEntry });

                HostsFile.Instance.Entries.MoveAfter(
                    new[] { this.dataGridViewHostsEntries.CurrentHostEntry },
                    this.dataGridViewHostsEntries.CurrentHostEntry);

                this.dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
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
            if (this.dataGridViewHostsEntries.SelectedRows.Count > 0)
            {
                var selectedEntries = this.dataGridViewHostsEntries.SelectedHostEntries.ToList();

                HostsFile.Instance.Entries.MoveBefore(
                     this.dataGridViewHostsEntries.SelectedHostEntries,
                     this.dataGridViewHostsEntries.LastSelectedHostEntry);

                this.dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
            }
            else if (this.dataGridViewHostsEntries.CurrentHostEntry != null)
            {
                var selectedEntries = new List<HostsEntry>(
                    new[] { this.dataGridViewHostsEntries.CurrentHostEntry });

                HostsFile.Instance.Entries.MoveBefore(
                    new[] { this.dataGridViewHostsEntries.CurrentHostEntry },
                    this.dataGridViewHostsEntries.CurrentHostEntry);

                this.dataGridViewHostsEntries.SelectedHostEntries = selectedEntries;
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
            if (this.dataGridViewHostsEntries.IsCurrentCellInEditMode)
            {
                var keys = this.menuPaste.ShortcutKeys;
                this.menuPaste.ShortcutKeys = Keys.None;
                this.menuContextPaste.ShortcutKeys = Keys.None;
                SendKeys.SendWait("^(V)");
                this.menuPaste.ShortcutKeys = keys;
                this.menuContextPaste.ShortcutKeys = keys;
                return;
            }

            this.dataGridViewHostsEntries.CancelEdit();

            if (this.dataGridViewHostsEntries.SelectedRows.Count > 0 && 
                this.clipboardEntries != null)
            {
                HostsFile.Instance.Entries.Insert(
                    this.dataGridViewHostsEntries.CurrentHostEntry, 
                    this.clipboardEntries);

                this.clipboardEntries = null;
            }
            else
            {
                foreach (
                    DataGridViewCell cell in 
                    this.dataGridViewHostsEntries.SelectedCells)
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
            this.ShowInTaskbar = this.Visible;
        }

        /// <summary>
        /// Updates the notify icon.
        /// </summary>
        private void UpdateNotifyIcon()
        {
            this.notifyIcon.Icon =
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
            if (this.dataGridViewHostsEntries.CurrentRow.DataBoundItem != null)
            {
                HostsFile.Instance.Entries.InsertBefore(
                    this.dataGridViewHostsEntries.CurrentHostEntry);
            }
            else
            {
                this.dataGridViewHostsEntries.CancelEdit();
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
            if (this.dataGridViewHostsEntries.CurrentRow.DataBoundItem != null)
            {
                HostsFile.Instance.Entries.InsertAfter(
                    this.dataGridViewHostsEntries.CurrentHostEntry);
            }
            else
            {
                this.dataGridViewHostsEntries.CancelEdit();
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

            this.menuViewArchive.Checked = !isChecked;
            this.buttonViewArchive.Checked = !isChecked;

            this.splitContainer.Panel2Collapsed = isChecked;
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
            HostsArchive archive = this.dataGridViewArchive.CurrentHostsArchive;

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
            HostsArchive archive = this.dataGridViewArchive.CurrentHostsArchive;

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

            this.menuPingIPs.Checked = !isChecked;
        }

        /// <summary>
        /// Called when remove default text clicked.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> 
        /// instance containing the event data.</param>
        private void OnRemoveDefaultTextClick(object sender, EventArgs e)
        {
            this.menuRemoveDefaultText.Checked = 
                !this.menuRemoveDefaultText.Checked;

            HostsFile.RemoveDefaultText = 
                this.menuRemoveDefaultText.Checked;
        }

        /// <summary>
        /// Loads the settings.
        /// </summary>
        private void LoadSettings()
        {
            Settings settings = Settings.Default;

            HostsEntry.AutoPingIPAddress = settings.AutoPingIPAddresses;
            HostsFile.RemoveDefaultText = settings.RemoveDefaultText;

            this.menuPingIPs.Checked = HostsEntry.AutoPingIPAddress;
            this.menuRemoveDefaultText.Checked = HostsFile.RemoveDefaultText;
            this.menuViewArchive.Checked = settings.ArchiveVisible;
            this.buttonViewArchive.Checked = settings.ArchiveVisible;
            this.splitContainer.Panel2Collapsed = !settings.ArchiveVisible;

            // Do quick check that saved location still exists for multi-monitor setups
            if (Screen.AllScreens.Any(screen =>
                screen.WorkingArea.Contains(settings.WindowLocation)))
            {
                this.Location = settings.WindowLocation;
                this.Size = settings.WindowSize;
            }

            this.splitContainer.SplitterDistance = settings.SplitterWidth;
        }

        /// <summary>
        /// Saves the settings.
        /// </summary>
        private void SaveSettings()
        {
            Settings settings = Settings.Default;

            settings.AutoPingIPAddresses = HostsEntry.AutoPingIPAddress;
            settings.RemoveDefaultText = HostsFile.RemoveDefaultText;
            settings.WindowLocation = this.Location;
            settings.ArchiveVisible = this.menuViewArchive.Checked;

            // Save size for normal window, don't save anything for minimized
            // since that's probably not what the user wants next time
            // they open
            if (this.WindowState == FormWindowState.Normal)
            {
                settings.WindowSize = this.Size;
                settings.WindowState = this.WindowState;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                settings.WindowState = this.WindowState;
            }

            settings.SplitterWidth = this.splitContainer.SplitterDistance;

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
            if (this.WindowState == FormWindowState.Normal)
            {
                Settings.Default.WindowSize = this.Size;
            }
        }

        /// <summary>
        /// Called when remove sort clicked.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void OnRemoveSortClick(object sender, EventArgs e)
        {
            this.hostEntriesView.RemoveSort();
        }

        /// <summary>
        /// Called when check clicked.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
        private void OnCheckClick(object sender, EventArgs e)
        {
            this.dataGridViewHostsEntries.CancelEdit();

            if (this.dataGridViewHostsEntries.SelectedRows.Count > 0)
            {
                HostsFile.Instance.Entries.SetEnabled(
                     this.dataGridViewHostsEntries.SelectedHostEntries,
                     isEnabled: true);
            }
            else if (this.dataGridViewHostsEntries.CurrentHostEntry != null)
            {
                HostsFile.Instance.Entries.SetEnabled(
                    new [] { this.dataGridViewHostsEntries.CurrentHostEntry },
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
            this.dataGridViewHostsEntries.CancelEdit();

            if (this.dataGridViewHostsEntries.SelectedRows.Count > 0)
            {
                HostsFile.Instance.Entries.SetEnabled(
                     this.dataGridViewHostsEntries.SelectedHostEntries,
                     isEnabled: false);
            }
            else if (this.dataGridViewHostsEntries.CurrentHostEntry != null)
            {
                HostsFile.Instance.Entries.SetEnabled(
                    new [] { this.dataGridViewHostsEntries.CurrentHostEntry },
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

        #endregion
    }
}