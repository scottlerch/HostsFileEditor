// <copyright file="MainForm.Designer.cs" company="N/A">
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
    /// <summary>
    /// MainForm designer class.
    /// </summary>
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (this.components != null))
            {
                this.components.Dispose();
            }

            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            System.Windows.Forms.ToolStripSeparator toolStripSeparator22;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator23;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator25;
            System.Windows.Forms.ToolStripLabel toolStripLabel1;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator27;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator18;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator14;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator13;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator12;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator28;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator4;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator5;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator6;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator24;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator9;
            System.Windows.Forms.ToolStripMenuItem menuFilter;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator15;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator26;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator16;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator17;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator19;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator21;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator20;
            System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
            this.toolStripContainer = new System.Windows.Forms.ToolStripContainer();
            this.statusStrip = new System.Windows.Forms.StatusStrip();
            this.labelLineCount = new System.Windows.Forms.ToolStripStatusLabel();
            this.labelLineCountNumber = new HostsFileEditor.Controls.ToolStripBindableStatusLabel();
            this.bindingSourceHostFile = new System.Windows.Forms.BindingSource(this.components);
            this.labelHostEntries = new System.Windows.Forms.ToolStripStatusLabel();
            this.labelHostEntriesCount = new HostsFileEditor.Controls.ToolStripBindableStatusLabel();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.dataGridViewHostsEntries = new HostsFileEditor.Controls.HostsEntryDataGridView();
            this.columnValid = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.columnEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.columnIpAddress = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnHostnames = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnComment = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnUnparsedText = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.columnFiller = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.contextMenuGrid = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuContextCut = new System.Windows.Forms.ToolStripMenuItem();
            this.menuContextCopy = new System.Windows.Forms.ToolStripMenuItem();
            this.menuContextPaste = new System.Windows.Forms.ToolStripMenuItem();
            this.menuContextDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.menuContextMoveUp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuContextMoveDown = new System.Windows.Forms.ToolStripMenuItem();
            this.menuContextInsertAbove = new System.Windows.Forms.ToolStripMenuItem();
            this.menuContextInsertBelow = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator31 = new System.Windows.Forms.ToolStripSeparator();
            this.contextMenuCheck = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuUncheck = new System.Windows.Forms.ToolStripMenuItem();
            this.bindingSourceView = new System.Windows.Forms.BindingSource(this.components);
            this.dataGridViewArchive = new HostsFileEditor.Controls.HostsArchiveDataGridView();
            this.fileNameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingSourceArchive = new System.Windows.Forms.BindingSource(this.components);
            this.toolStripArchive = new HostsFileEditor.Controls.ToolStripEx();
            this.buttonDeleteArchive = new System.Windows.Forms.ToolStripButton();
            this.buttonLoadArchive = new System.Windows.Forms.ToolStripButton();
            this.menuStrip = new HostsFileEditor.Controls.MenuStripEx();
            this.menuFile = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSave = new System.Windows.Forms.ToolStripMenuItem();
            this.menuSaveAs = new System.Windows.Forms.ToolStripMenuItem();
            this.menuArchive = new System.Windows.Forms.ToolStripMenuItem();
            this.openTextEditor = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRestoreDefaults = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDisable = new System.Windows.Forms.ToolStripMenuItem();
            this.menuImport = new System.Windows.Forms.ToolStripMenuItem();
            this.menuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.editToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRefresh = new System.Windows.Forms.ToolStripMenuItem();
            this.menuUndo = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRedo = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCut = new System.Windows.Forms.ToolStripMenuItem();
            this.menuCopy = new System.Windows.Forms.ToolStripMenuItem();
            this.menuPaste = new System.Windows.Forms.ToolStripMenuItem();
            this.menuDelete = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveUp = new System.Windows.Forms.ToolStripMenuItem();
            this.menuMoveDown = new System.Windows.Forms.ToolStripMenuItem();
            this.insertRowAboveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.insertRowBelowToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator30 = new System.Windows.Forms.ToolStripSeparator();
            this.menuCheck = new System.Windows.Forms.ToolStripMenuItem();
            this.menuUncheck = new System.Windows.Forms.ToolStripMenuItem();
            this.viewToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.menuViewArchive = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFilterComments = new System.Windows.Forms.ToolStripMenuItem();
            this.menuFilterDisabled = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator29 = new System.Windows.Forms.ToolStripSeparator();
            this.menuRemoveSort = new System.Windows.Forms.ToolStripMenuItem();
            this.menuTools = new System.Windows.Forms.ToolStripMenuItem();
            this.menuPingIPs = new System.Windows.Forms.ToolStripMenuItem();
            this.menuRemoveDefaultText = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStrip = new HostsFileEditor.Controls.ToolStripEx();
            this.buttonSave = new System.Windows.Forms.ToolStripButton();
            this.buttonRefresh = new System.Windows.Forms.ToolStripButton();
            this.buttonDisable = new System.Windows.Forms.ToolStripButton();
            this.buttonArchive = new System.Windows.Forms.ToolStripButton();
            this.buttonViewArchive = new System.Windows.Forms.ToolStripButton();
            this.toolStripDropDownButton1 = new System.Windows.Forms.ToolStripLabel();
            this.buttonFilterComment = new System.Windows.Forms.ToolStripButton();
            this.buttonFilterDisabled = new System.Windows.Forms.ToolStripButton();
            this.textFilter = new HostsFileEditor.Controls.ToolStripSpringTextBox();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator7 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator8 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator10 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripSeparator11 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripStatusLabel1 = new System.Windows.Forms.ToolStripStatusLabel();
            this.toolStripButton1 = new System.Windows.Forms.ToolStripButton();
            this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.notifyIcon = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuTray = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuContextEdit = new System.Windows.Forms.ToolStripMenuItem();
            this.menuContextDisable = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuExit = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.bindingSourceHostEntries = new System.Windows.Forms.BindingSource(this.components);
            this.helpToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator22 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator23 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator25 = new System.Windows.Forms.ToolStripSeparator();
            toolStripLabel1 = new System.Windows.Forms.ToolStripLabel();
            toolStripSeparator27 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator18 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator14 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator13 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator12 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator28 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator6 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator24 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator9 = new System.Windows.Forms.ToolStripSeparator();
            menuFilter = new System.Windows.Forms.ToolStripMenuItem();
            toolStripSeparator15 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator26 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator16 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator17 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator19 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator21 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator20 = new System.Windows.Forms.ToolStripSeparator();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.toolStripContainer.BottomToolStripPanel.SuspendLayout();
            this.toolStripContainer.ContentPanel.SuspendLayout();
            this.toolStripContainer.TopToolStripPanel.SuspendLayout();
            this.toolStripContainer.SuspendLayout();
            this.statusStrip.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceHostFile)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).BeginInit();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewHostsEntries)).BeginInit();
            this.contextMenuGrid.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceView)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewArchive)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceArchive)).BeginInit();
            this.toolStripArchive.SuspendLayout();
            this.menuStrip.SuspendLayout();
            this.toolStrip.SuspendLayout();
            this.contextMenuTray.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceHostEntries)).BeginInit();
            this.SuspendLayout();
            // 
            // toolStripContainer
            // 
            // 
            // toolStripContainer.BottomToolStripPanel
            // 
            this.toolStripContainer.BottomToolStripPanel.Controls.Add(this.statusStrip);
            // 
            // toolStripContainer.ContentPanel
            // 
            this.toolStripContainer.ContentPanel.Controls.Add(this.splitContainer);
            resources.ApplyResources(this.toolStripContainer.ContentPanel, "toolStripContainer.ContentPanel");
            resources.ApplyResources(this.toolStripContainer, "toolStripContainer");
            this.toolStripContainer.Name = "toolStripContainer";
            // 
            // toolStripContainer.TopToolStripPanel
            // 
            this.toolStripContainer.TopToolStripPanel.Controls.Add(this.menuStrip);
            this.toolStripContainer.TopToolStripPanel.Controls.Add(this.toolStrip);
            // 
            // statusStrip
            // 
            resources.ApplyResources(this.statusStrip, "statusStrip");
            this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.labelLineCount,
            this.labelLineCountNumber,
            this.labelHostEntries,
            this.labelHostEntriesCount});
            this.statusStrip.Name = "statusStrip";
            // 
            // labelLineCount
            // 
            this.labelLineCount.Name = "labelLineCount";
            resources.ApplyResources(this.labelLineCount, "labelLineCount");
            // 
            // labelLineCountNumber
            // 
            this.labelLineCountNumber.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.bindingSourceHostFile, "LineCount", true));
            this.labelLineCountNumber.Name = "labelLineCountNumber";
            resources.ApplyResources(this.labelLineCountNumber, "labelLineCountNumber");
            // 
            // bindingSourceHostFile
            // 
            this.bindingSourceHostFile.DataSource = typeof(HostsFileEditor.HostsFile);
            // 
            // labelHostEntries
            // 
            this.labelHostEntries.Name = "labelHostEntries";
            resources.ApplyResources(this.labelHostEntries, "labelHostEntries");
            // 
            // labelHostEntriesCount
            // 
            this.labelHostEntriesCount.DataBindings.Add(new System.Windows.Forms.Binding("Text", this.bindingSourceHostFile, "EnabledCount", true));
            this.labelHostEntriesCount.Name = "labelHostEntriesCount";
            resources.ApplyResources(this.labelHostEntriesCount, "labelHostEntriesCount");
            // 
            // splitContainer
            // 
            resources.ApplyResources(this.splitContainer, "splitContainer");
            this.splitContainer.FixedPanel = System.Windows.Forms.FixedPanel.Panel2;
            this.splitContainer.Name = "splitContainer";
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.dataGridViewHostsEntries);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.dataGridViewArchive);
            this.splitContainer.Panel2.Controls.Add(this.toolStripArchive);
            // 
            // dataGridViewHostsEntries
            // 
            this.dataGridViewHostsEntries.AllowDrop = true;
            this.dataGridViewHostsEntries.AllowUserToOrderColumns = true;
            this.dataGridViewHostsEntries.AllowUserToResizeRows = false;
            this.dataGridViewHostsEntries.AutoGenerateColumns = false;
            this.dataGridViewHostsEntries.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridViewHostsEntries.ClearSort = null;
            this.dataGridViewHostsEntries.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewHostsEntries.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.columnValid,
            this.columnEnabled,
            this.columnIpAddress,
            this.columnHostnames,
            this.columnComment,
            this.columnUnparsedText,
            this.columnFiller});
            this.dataGridViewHostsEntries.ContextMenuStrip = this.contextMenuGrid;
            this.dataGridViewHostsEntries.DataSource = this.bindingSourceView;
            resources.ApplyResources(this.dataGridViewHostsEntries, "dataGridViewHostsEntries");
            this.dataGridViewHostsEntries.Name = "dataGridViewHostsEntries";
            // 
            // columnValid
            // 
            this.columnValid.DataPropertyName = "Valid";
            resources.ApplyResources(this.columnValid, "columnValid");
            this.columnValid.Name = "columnValid";
            this.columnValid.ReadOnly = true;
            this.columnValid.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // columnEnabled
            // 
            this.columnEnabled.DataPropertyName = "Enabled";
            resources.ApplyResources(this.columnEnabled, "columnEnabled");
            this.columnEnabled.Name = "columnEnabled";
            this.columnEnabled.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Automatic;
            // 
            // columnIpAddress
            // 
            this.columnIpAddress.DataPropertyName = "IpAddress";
            resources.ApplyResources(this.columnIpAddress, "columnIpAddress");
            this.columnIpAddress.Name = "columnIpAddress";
            // 
            // columnHostnames
            // 
            this.columnHostnames.DataPropertyName = "HostNames";
            resources.ApplyResources(this.columnHostnames, "columnHostnames");
            this.columnHostnames.Name = "columnHostnames";
            // 
            // columnComment
            // 
            this.columnComment.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.columnComment.DataPropertyName = "Comment";
            resources.ApplyResources(this.columnComment, "columnComment");
            this.columnComment.Name = "columnComment";
            // 
            // columnUnparsedText
            // 
            this.columnUnparsedText.DataPropertyName = "UnparsedText";
            resources.ApplyResources(this.columnUnparsedText, "columnUnparsedText");
            this.columnUnparsedText.Name = "columnUnparsedText";
            this.columnUnparsedText.ReadOnly = true;
            // 
            // columnFiller
            // 
            this.columnFiller.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            resources.ApplyResources(this.columnFiller, "columnFiller");
            this.columnFiller.Name = "columnFiller";
            this.columnFiller.ReadOnly = true;
            // 
            // contextMenuGrid
            // 
            this.contextMenuGrid.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuContextCut,
            this.menuContextCopy,
            this.menuContextPaste,
            toolStripSeparator22,
            this.menuContextDelete,
            toolStripSeparator23,
            this.menuContextMoveUp,
            this.menuContextMoveDown,
            toolStripSeparator25,
            this.menuContextInsertAbove,
            this.menuContextInsertBelow,
            this.toolStripSeparator31,
            this.contextMenuCheck,
            this.contextMenuUncheck});
            this.contextMenuGrid.Name = "contextMenuGrid";
            resources.ApplyResources(this.contextMenuGrid, "contextMenuGrid");
            // 
            // menuContextCut
            // 
            this.menuContextCut.Image = global::HostsFileEditor.Properties.Resources.Cut;
            this.menuContextCut.Name = "menuContextCut";
            resources.ApplyResources(this.menuContextCut, "menuContextCut");
            this.menuContextCut.Click += new System.EventHandler(this.OnCutClick);
            // 
            // menuContextCopy
            // 
            this.menuContextCopy.Image = global::HostsFileEditor.Properties.Resources.Copy;
            this.menuContextCopy.Name = "menuContextCopy";
            resources.ApplyResources(this.menuContextCopy, "menuContextCopy");
            this.menuContextCopy.Click += new System.EventHandler(this.OnCopyClick);
            // 
            // menuContextPaste
            // 
            this.menuContextPaste.Image = global::HostsFileEditor.Properties.Resources.Paste;
            this.menuContextPaste.Name = "menuContextPaste";
            resources.ApplyResources(this.menuContextPaste, "menuContextPaste");
            this.menuContextPaste.Click += new System.EventHandler(this.OnPasteClick);
            // 
            // toolStripSeparator22
            // 
            toolStripSeparator22.Name = "toolStripSeparator22";
            resources.ApplyResources(toolStripSeparator22, "toolStripSeparator22");
            // 
            // menuContextDelete
            // 
            this.menuContextDelete.Image = global::HostsFileEditor.Properties.Resources.Delete;
            this.menuContextDelete.Name = "menuContextDelete";
            resources.ApplyResources(this.menuContextDelete, "menuContextDelete");
            this.menuContextDelete.Click += new System.EventHandler(this.OnDeleteClick);
            // 
            // toolStripSeparator23
            // 
            toolStripSeparator23.Name = "toolStripSeparator23";
            resources.ApplyResources(toolStripSeparator23, "toolStripSeparator23");
            // 
            // menuContextMoveUp
            // 
            this.menuContextMoveUp.Image = global::HostsFileEditor.Properties.Resources.MoveUp;
            this.menuContextMoveUp.Name = "menuContextMoveUp";
            resources.ApplyResources(this.menuContextMoveUp, "menuContextMoveUp");
            this.menuContextMoveUp.Click += new System.EventHandler(this.OnMoveUpClick);
            // 
            // menuContextMoveDown
            // 
            this.menuContextMoveDown.Image = global::HostsFileEditor.Properties.Resources.MoveDown;
            this.menuContextMoveDown.Name = "menuContextMoveDown";
            resources.ApplyResources(this.menuContextMoveDown, "menuContextMoveDown");
            this.menuContextMoveDown.Click += new System.EventHandler(this.OnMoveDownClick);
            // 
            // toolStripSeparator25
            // 
            toolStripSeparator25.Name = "toolStripSeparator25";
            resources.ApplyResources(toolStripSeparator25, "toolStripSeparator25");
            // 
            // menuContextInsertAbove
            // 
            this.menuContextInsertAbove.Image = global::HostsFileEditor.Properties.Resources.InsertAbove;
            this.menuContextInsertAbove.Name = "menuContextInsertAbove";
            resources.ApplyResources(this.menuContextInsertAbove, "menuContextInsertAbove");
            this.menuContextInsertAbove.Click += new System.EventHandler(this.OnInsertAboveClick);
            // 
            // menuContextInsertBelow
            // 
            this.menuContextInsertBelow.Image = global::HostsFileEditor.Properties.Resources.InsertBelow;
            this.menuContextInsertBelow.Name = "menuContextInsertBelow";
            resources.ApplyResources(this.menuContextInsertBelow, "menuContextInsertBelow");
            this.menuContextInsertBelow.Click += new System.EventHandler(this.OnInsertBelowClick);
            // 
            // toolStripSeparator31
            // 
            this.toolStripSeparator31.Name = "toolStripSeparator31";
            resources.ApplyResources(this.toolStripSeparator31, "toolStripSeparator31");
            // 
            // contextMenuCheck
            // 
            this.contextMenuCheck.Image = global::HostsFileEditor.Properties.Resources.Check;
            resources.ApplyResources(this.contextMenuCheck, "contextMenuCheck");
            this.contextMenuCheck.Name = "contextMenuCheck";
            this.contextMenuCheck.Click += new System.EventHandler(this.OnCheckClick);
            // 
            // contextMenuUncheck
            // 
            this.contextMenuUncheck.Image = global::HostsFileEditor.Properties.Resources.Uncheck;
            resources.ApplyResources(this.contextMenuUncheck, "contextMenuUncheck");
            this.contextMenuUncheck.Name = "contextMenuUncheck";
            this.contextMenuUncheck.Click += new System.EventHandler(this.OnUncheckClick);
            // 
            // bindingSourceView
            // 
            this.bindingSourceView.DataSource = typeof(HostsFileEditor.HostsEntryList);
            // 
            // dataGridViewArchive
            // 
            this.dataGridViewArchive.AllowUserToAddRows = false;
            this.dataGridViewArchive.AllowUserToDeleteRows = false;
            this.dataGridViewArchive.AllowUserToResizeColumns = false;
            this.dataGridViewArchive.AllowUserToResizeRows = false;
            this.dataGridViewArchive.AutoGenerateColumns = false;
            this.dataGridViewArchive.BackgroundColor = System.Drawing.Color.White;
            this.dataGridViewArchive.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.dataGridViewArchive.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewArchive.ColumnHeadersVisible = false;
            this.dataGridViewArchive.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.fileNameDataGridViewTextBoxColumn});
            this.dataGridViewArchive.DataSource = this.bindingSourceArchive;
            resources.ApplyResources(this.dataGridViewArchive, "dataGridViewArchive");
            this.dataGridViewArchive.GridColor = System.Drawing.Color.White;
            this.dataGridViewArchive.MultiSelect = false;
            this.dataGridViewArchive.Name = "dataGridViewArchive";
            this.dataGridViewArchive.ReadOnly = true;
            this.dataGridViewArchive.RowHeadersVisible = false;
            this.dataGridViewArchive.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            // 
            // fileNameDataGridViewTextBoxColumn
            // 
            this.fileNameDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.fileNameDataGridViewTextBoxColumn.DataPropertyName = "FileName";
            resources.ApplyResources(this.fileNameDataGridViewTextBoxColumn, "fileNameDataGridViewTextBoxColumn");
            this.fileNameDataGridViewTextBoxColumn.Name = "fileNameDataGridViewTextBoxColumn";
            this.fileNameDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // bindingSourceArchive
            // 
            this.bindingSourceArchive.AllowNew = false;
            this.bindingSourceArchive.DataSource = typeof(HostsFileEditor.HostsArchiveList);
            // 
            // toolStripArchive
            // 
            this.toolStripArchive.ClickThrough = true;
            this.toolStripArchive.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripArchive.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            toolStripLabel1,
            toolStripSeparator27,
            this.buttonDeleteArchive,
            this.buttonLoadArchive});
            resources.ApplyResources(this.toolStripArchive, "toolStripArchive");
            this.toolStripArchive.Name = "toolStripArchive";
            // 
            // toolStripLabel1
            // 
            toolStripLabel1.Name = "toolStripLabel1";
            resources.ApplyResources(toolStripLabel1, "toolStripLabel1");
            // 
            // toolStripSeparator27
            // 
            toolStripSeparator27.Name = "toolStripSeparator27";
            resources.ApplyResources(toolStripSeparator27, "toolStripSeparator27");
            // 
            // buttonDeleteArchive
            // 
            this.buttonDeleteArchive.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.buttonDeleteArchive.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.buttonDeleteArchive.Image = global::HostsFileEditor.Properties.Resources.Delete;
            resources.ApplyResources(this.buttonDeleteArchive, "buttonDeleteArchive");
            this.buttonDeleteArchive.Name = "buttonDeleteArchive";
            this.buttonDeleteArchive.Click += new System.EventHandler(this.OnArchiveDeleteClick);
            // 
            // buttonLoadArchive
            // 
            this.buttonLoadArchive.Alignment = System.Windows.Forms.ToolStripItemAlignment.Right;
            this.buttonLoadArchive.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.buttonLoadArchive.Image = global::HostsFileEditor.Properties.Resources.LoadArchive;
            resources.ApplyResources(this.buttonLoadArchive, "buttonLoadArchive");
            this.buttonLoadArchive.Name = "buttonLoadArchive";
            this.buttonLoadArchive.Click += new System.EventHandler(this.OnArchiveLoadClick);
            // 
            // menuStrip
            // 
            this.menuStrip.ClickThrough = true;
            resources.ApplyResources(this.menuStrip, "menuStrip");
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuFile,
            this.editToolStripMenuItem,
            this.viewToolStripMenuItem,
            this.menuTools,
            this.helpToolStripMenuItem});
            this.menuStrip.Name = "menuStrip";
            // 
            // menuFile
            // 
            this.menuFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuSave,
            this.menuSaveAs,
            toolStripSeparator18,
            this.menuArchive,
            toolStripSeparator14,
            this.openTextEditor,
            this.menuRestoreDefaults,
            this.menuDisable,
            toolStripSeparator13,
            this.menuImport,
            toolStripSeparator12,
            this.menuExit});
            this.menuFile.Name = "menuFile";
            resources.ApplyResources(this.menuFile, "menuFile");
            // 
            // menuSave
            // 
            this.menuSave.Image = global::HostsFileEditor.Properties.Resources.Save;
            this.menuSave.Name = "menuSave";
            resources.ApplyResources(this.menuSave, "menuSave");
            this.menuSave.Click += new System.EventHandler(this.OnSaveClick);
            // 
            // menuSaveAs
            // 
            this.menuSaveAs.Name = "menuSaveAs";
            resources.ApplyResources(this.menuSaveAs, "menuSaveAs");
            this.menuSaveAs.Click += new System.EventHandler(this.OnSaveAsClick);
            // 
            // toolStripSeparator18
            // 
            toolStripSeparator18.Name = "toolStripSeparator18";
            resources.ApplyResources(toolStripSeparator18, "toolStripSeparator18");
            // 
            // menuArchive
            // 
            this.menuArchive.Image = global::HostsFileEditor.Properties.Resources.Archive;
            this.menuArchive.Name = "menuArchive";
            resources.ApplyResources(this.menuArchive, "menuArchive");
            this.menuArchive.Click += new System.EventHandler(this.OnArchiveClick);
            // 
            // toolStripSeparator14
            // 
            toolStripSeparator14.Name = "toolStripSeparator14";
            resources.ApplyResources(toolStripSeparator14, "toolStripSeparator14");
            // 
            // openTextEditor
            // 
            this.openTextEditor.Name = "openTextEditor";
            resources.ApplyResources(this.openTextEditor, "openTextEditor");
            this.openTextEditor.Click += new System.EventHandler(this.OnOpenTextEditorClick);
            // 
            // menuRestoreDefaults
            // 
            this.menuRestoreDefaults.Name = "menuRestoreDefaults";
            resources.ApplyResources(this.menuRestoreDefaults, "menuRestoreDefaults");
            this.menuRestoreDefaults.Click += new System.EventHandler(this.OnRestoreClick);
            // 
            // menuDisable
            // 
            this.menuDisable.Image = global::HostsFileEditor.Properties.Resources.Disable;
            this.menuDisable.Name = "menuDisable";
            resources.ApplyResources(this.menuDisable, "menuDisable");
            this.menuDisable.Click += new System.EventHandler(this.OnDisableHostsClick);
            // 
            // toolStripSeparator13
            // 
            toolStripSeparator13.Name = "toolStripSeparator13";
            resources.ApplyResources(toolStripSeparator13, "toolStripSeparator13");
            // 
            // menuImport
            // 
            this.menuImport.Image = global::HostsFileEditor.Properties.Resources.Import;
            this.menuImport.Name = "menuImport";
            resources.ApplyResources(this.menuImport, "menuImport");
            this.menuImport.Click += new System.EventHandler(this.OnImportClick);
            // 
            // toolStripSeparator12
            // 
            toolStripSeparator12.Name = "toolStripSeparator12";
            resources.ApplyResources(toolStripSeparator12, "toolStripSeparator12");
            // 
            // menuExit
            // 
            this.menuExit.Name = "menuExit";
            resources.ApplyResources(this.menuExit, "menuExit");
            this.menuExit.Click += new System.EventHandler(this.OnExitClick);
            // 
            // editToolStripMenuItem
            // 
            this.editToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuRefresh,
            toolStripSeparator28,
            this.menuUndo,
            this.menuRedo,
            toolStripSeparator4,
            this.menuCut,
            this.menuCopy,
            this.menuPaste,
            toolStripSeparator5,
            this.menuDelete,
            toolStripSeparator6,
            this.menuMoveUp,
            this.menuMoveDown,
            toolStripSeparator24,
            this.insertRowAboveToolStripMenuItem,
            this.insertRowBelowToolStripMenuItem,
            this.toolStripSeparator30,
            this.menuCheck,
            this.menuUncheck});
            this.editToolStripMenuItem.Name = "editToolStripMenuItem";
            resources.ApplyResources(this.editToolStripMenuItem, "editToolStripMenuItem");
            // 
            // menuRefresh
            // 
            this.menuRefresh.Image = global::HostsFileEditor.Properties.Resources.Refresh;
            resources.ApplyResources(this.menuRefresh, "menuRefresh");
            this.menuRefresh.Name = "menuRefresh";
            this.menuRefresh.Click += new System.EventHandler(this.OnRefreshClick);
            // 
            // toolStripSeparator28
            // 
            toolStripSeparator28.Name = "toolStripSeparator28";
            resources.ApplyResources(toolStripSeparator28, "toolStripSeparator28");
            // 
            // menuUndo
            // 
            this.menuUndo.Image = global::HostsFileEditor.Properties.Resources.Undo;
            this.menuUndo.Name = "menuUndo";
            resources.ApplyResources(this.menuUndo, "menuUndo");
            this.menuUndo.Click += new System.EventHandler(this.OnUndoClick);
            // 
            // menuRedo
            // 
            this.menuRedo.Image = global::HostsFileEditor.Properties.Resources.Redo;
            this.menuRedo.Name = "menuRedo";
            resources.ApplyResources(this.menuRedo, "menuRedo");
            this.menuRedo.Click += new System.EventHandler(this.OnRedoClick);
            // 
            // toolStripSeparator4
            // 
            toolStripSeparator4.Name = "toolStripSeparator4";
            resources.ApplyResources(toolStripSeparator4, "toolStripSeparator4");
            // 
            // menuCut
            // 
            this.menuCut.Image = global::HostsFileEditor.Properties.Resources.Cut;
            this.menuCut.Name = "menuCut";
            resources.ApplyResources(this.menuCut, "menuCut");
            this.menuCut.Click += new System.EventHandler(this.OnCutClick);
            // 
            // menuCopy
            // 
            this.menuCopy.Image = global::HostsFileEditor.Properties.Resources.Copy;
            this.menuCopy.Name = "menuCopy";
            resources.ApplyResources(this.menuCopy, "menuCopy");
            this.menuCopy.Click += new System.EventHandler(this.OnCopyClick);
            // 
            // menuPaste
            // 
            this.menuPaste.Image = global::HostsFileEditor.Properties.Resources.Paste;
            this.menuPaste.Name = "menuPaste";
            resources.ApplyResources(this.menuPaste, "menuPaste");
            this.menuPaste.Click += new System.EventHandler(this.OnPasteClick);
            // 
            // toolStripSeparator5
            // 
            toolStripSeparator5.Name = "toolStripSeparator5";
            resources.ApplyResources(toolStripSeparator5, "toolStripSeparator5");
            // 
            // menuDelete
            // 
            this.menuDelete.Image = global::HostsFileEditor.Properties.Resources.Delete;
            this.menuDelete.Name = "menuDelete";
            resources.ApplyResources(this.menuDelete, "menuDelete");
            this.menuDelete.Click += new System.EventHandler(this.OnDeleteClick);
            // 
            // toolStripSeparator6
            // 
            toolStripSeparator6.Name = "toolStripSeparator6";
            resources.ApplyResources(toolStripSeparator6, "toolStripSeparator6");
            // 
            // menuMoveUp
            // 
            this.menuMoveUp.Image = global::HostsFileEditor.Properties.Resources.MoveUp;
            this.menuMoveUp.Name = "menuMoveUp";
            resources.ApplyResources(this.menuMoveUp, "menuMoveUp");
            this.menuMoveUp.Click += new System.EventHandler(this.OnMoveUpClick);
            // 
            // menuMoveDown
            // 
            this.menuMoveDown.Image = global::HostsFileEditor.Properties.Resources.MoveDown;
            this.menuMoveDown.Name = "menuMoveDown";
            resources.ApplyResources(this.menuMoveDown, "menuMoveDown");
            this.menuMoveDown.Click += new System.EventHandler(this.OnMoveDownClick);
            // 
            // toolStripSeparator24
            // 
            toolStripSeparator24.Name = "toolStripSeparator24";
            resources.ApplyResources(toolStripSeparator24, "toolStripSeparator24");
            // 
            // insertRowAboveToolStripMenuItem
            // 
            this.insertRowAboveToolStripMenuItem.Image = global::HostsFileEditor.Properties.Resources.InsertAbove;
            this.insertRowAboveToolStripMenuItem.Name = "insertRowAboveToolStripMenuItem";
            resources.ApplyResources(this.insertRowAboveToolStripMenuItem, "insertRowAboveToolStripMenuItem");
            this.insertRowAboveToolStripMenuItem.Click += new System.EventHandler(this.OnInsertAboveClick);
            // 
            // insertRowBelowToolStripMenuItem
            // 
            this.insertRowBelowToolStripMenuItem.Image = global::HostsFileEditor.Properties.Resources.InsertBelow;
            this.insertRowBelowToolStripMenuItem.Name = "insertRowBelowToolStripMenuItem";
            resources.ApplyResources(this.insertRowBelowToolStripMenuItem, "insertRowBelowToolStripMenuItem");
            this.insertRowBelowToolStripMenuItem.Click += new System.EventHandler(this.OnInsertBelowClick);
            // 
            // toolStripSeparator30
            // 
            this.toolStripSeparator30.Name = "toolStripSeparator30";
            resources.ApplyResources(this.toolStripSeparator30, "toolStripSeparator30");
            // 
            // menuCheck
            // 
            this.menuCheck.Image = global::HostsFileEditor.Properties.Resources.Check;
            resources.ApplyResources(this.menuCheck, "menuCheck");
            this.menuCheck.Name = "menuCheck";
            this.menuCheck.Click += new System.EventHandler(this.OnCheckClick);
            // 
            // menuUncheck
            // 
            this.menuUncheck.Image = global::HostsFileEditor.Properties.Resources.Uncheck;
            resources.ApplyResources(this.menuUncheck, "menuUncheck");
            this.menuUncheck.Name = "menuUncheck";
            this.menuUncheck.Click += new System.EventHandler(this.OnUncheckClick);
            // 
            // viewToolStripMenuItem
            // 
            this.viewToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuViewArchive,
            toolStripSeparator9,
            menuFilter,
            this.toolStripSeparator29,
            this.menuRemoveSort});
            this.viewToolStripMenuItem.Name = "viewToolStripMenuItem";
            resources.ApplyResources(this.viewToolStripMenuItem, "viewToolStripMenuItem");
            // 
            // menuViewArchive
            // 
            this.menuViewArchive.Image = global::HostsFileEditor.Properties.Resources.ViewArchive;
            this.menuViewArchive.Name = "menuViewArchive";
            resources.ApplyResources(this.menuViewArchive, "menuViewArchive");
            this.menuViewArchive.Click += new System.EventHandler(this.OnViewArchiveClick);
            // 
            // toolStripSeparator9
            // 
            toolStripSeparator9.Name = "toolStripSeparator9";
            resources.ApplyResources(toolStripSeparator9, "toolStripSeparator9");
            // 
            // menuFilter
            // 
            menuFilter.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuFilterComments,
            this.menuFilterDisabled});
            menuFilter.Image = global::HostsFileEditor.Properties.Resources.Filter;
            menuFilter.Name = "menuFilter";
            resources.ApplyResources(menuFilter, "menuFilter");
            // 
            // menuFilterComments
            // 
            this.menuFilterComments.Image = global::HostsFileEditor.Properties.Resources.FilterComments;
            this.menuFilterComments.Name = "menuFilterComments";
            resources.ApplyResources(this.menuFilterComments, "menuFilterComments");
            this.menuFilterComments.Click += new System.EventHandler(this.OnFilterCommentClick);
            // 
            // menuFilterDisabled
            // 
            this.menuFilterDisabled.Image = global::HostsFileEditor.Properties.Resources.FilterDisabled;
            this.menuFilterDisabled.Name = "menuFilterDisabled";
            resources.ApplyResources(this.menuFilterDisabled, "menuFilterDisabled");
            this.menuFilterDisabled.Click += new System.EventHandler(this.OnFilterDisabledClick);
            // 
            // toolStripSeparator29
            // 
            this.toolStripSeparator29.Name = "toolStripSeparator29";
            resources.ApplyResources(this.toolStripSeparator29, "toolStripSeparator29");
            // 
            // menuRemoveSort
            // 
            this.menuRemoveSort.Name = "menuRemoveSort";
            resources.ApplyResources(this.menuRemoveSort, "menuRemoveSort");
            this.menuRemoveSort.Click += new System.EventHandler(this.OnRemoveSortClick);
            // 
            // menuTools
            // 
            this.menuTools.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuPingIPs,
            this.menuRemoveDefaultText});
            this.menuTools.Name = "menuTools";
            resources.ApplyResources(this.menuTools, "menuTools");
            // 
            // menuPingIPs
            // 
            this.menuPingIPs.Name = "menuPingIPs";
            resources.ApplyResources(this.menuPingIPs, "menuPingIPs");
            this.menuPingIPs.Click += new System.EventHandler(this.OnPingIPsClick);
            // 
            // menuRemoveDefaultText
            // 
            this.menuRemoveDefaultText.Checked = true;
            this.menuRemoveDefaultText.CheckState = System.Windows.Forms.CheckState.Checked;
            this.menuRemoveDefaultText.Name = "menuRemoveDefaultText";
            resources.ApplyResources(this.menuRemoveDefaultText, "menuRemoveDefaultText");
            this.menuRemoveDefaultText.Click += new System.EventHandler(this.OnRemoveDefaultTextClick);
            // 
            // toolStrip
            // 
            this.toolStrip.ClickThrough = true;
            resources.ApplyResources(this.toolStrip, "toolStrip");
            this.toolStrip.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.buttonSave,
            toolStripSeparator15,
            this.buttonRefresh,
            toolStripSeparator26,
            this.buttonDisable,
            toolStripSeparator16,
            this.buttonArchive,
            this.buttonViewArchive,
            toolStripSeparator17,
            this.toolStripDropDownButton1,
            this.buttonFilterComment,
            this.buttonFilterDisabled,
            this.textFilter,
            toolStripSeparator19});
            this.toolStrip.Name = "toolStrip";
            this.toolStrip.Stretch = true;
            this.toolStrip.TextChanged += new System.EventHandler(this.OnFilterTextChanged);
            // 
            // buttonSave
            // 
            this.buttonSave.Image = global::HostsFileEditor.Properties.Resources.Save;
            resources.ApplyResources(this.buttonSave, "buttonSave");
            this.buttonSave.Margin = new System.Windows.Forms.Padding(2, 1, 0, 2);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Click += new System.EventHandler(this.OnSaveClick);
            // 
            // toolStripSeparator15
            // 
            toolStripSeparator15.Name = "toolStripSeparator15";
            resources.ApplyResources(toolStripSeparator15, "toolStripSeparator15");
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Image = global::HostsFileEditor.Properties.Resources.Refresh;
            resources.ApplyResources(this.buttonRefresh, "buttonRefresh");
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Click += new System.EventHandler(this.OnRefreshClick);
            // 
            // toolStripSeparator26
            // 
            toolStripSeparator26.Name = "toolStripSeparator26";
            resources.ApplyResources(toolStripSeparator26, "toolStripSeparator26");
            // 
            // buttonDisable
            // 
            this.buttonDisable.Image = global::HostsFileEditor.Properties.Resources.Disable;
            resources.ApplyResources(this.buttonDisable, "buttonDisable");
            this.buttonDisable.Name = "buttonDisable";
            this.buttonDisable.Click += new System.EventHandler(this.OnDisableHostsClick);
            // 
            // toolStripSeparator16
            // 
            toolStripSeparator16.Name = "toolStripSeparator16";
            resources.ApplyResources(toolStripSeparator16, "toolStripSeparator16");
            // 
            // buttonArchive
            // 
            this.buttonArchive.Image = global::HostsFileEditor.Properties.Resources.Archive;
            resources.ApplyResources(this.buttonArchive, "buttonArchive");
            this.buttonArchive.Name = "buttonArchive";
            this.buttonArchive.Click += new System.EventHandler(this.OnArchiveClick);
            // 
            // buttonViewArchive
            // 
            this.buttonViewArchive.Image = global::HostsFileEditor.Properties.Resources.ViewArchive;
            resources.ApplyResources(this.buttonViewArchive, "buttonViewArchive");
            this.buttonViewArchive.Name = "buttonViewArchive";
            this.buttonViewArchive.Click += new System.EventHandler(this.OnViewArchiveClick);
            // 
            // toolStripSeparator17
            // 
            toolStripSeparator17.Name = "toolStripSeparator17";
            resources.ApplyResources(toolStripSeparator17, "toolStripSeparator17");
            // 
            // toolStripDropDownButton1
            // 
            this.toolStripDropDownButton1.Image = global::HostsFileEditor.Properties.Resources.Filter;
            resources.ApplyResources(this.toolStripDropDownButton1, "toolStripDropDownButton1");
            this.toolStripDropDownButton1.Name = "toolStripDropDownButton1";
            // 
            // buttonFilterComment
            // 
            this.buttonFilterComment.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.buttonFilterComment.Image = global::HostsFileEditor.Properties.Resources.FilterComments;
            this.buttonFilterComment.Name = "buttonFilterComment";
            resources.ApplyResources(this.buttonFilterComment, "buttonFilterComment");
            this.buttonFilterComment.Click += new System.EventHandler(this.OnFilterCommentClick);
            // 
            // buttonFilterDisabled
            // 
            this.buttonFilterDisabled.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.buttonFilterDisabled.Image = global::HostsFileEditor.Properties.Resources.FilterDisabled;
            this.buttonFilterDisabled.Name = "buttonFilterDisabled";
            resources.ApplyResources(this.buttonFilterDisabled, "buttonFilterDisabled");
            this.buttonFilterDisabled.Click += new System.EventHandler(this.OnFilterDisabledClick);
            // 
            // textFilter
            // 
            this.textFilter.Name = "textFilter";
            resources.ApplyResources(this.textFilter, "textFilter");
            this.textFilter.TextChanged += new System.EventHandler(this.OnFilterTextChanged);
            // 
            // toolStripSeparator19
            // 
            toolStripSeparator19.Name = "toolStripSeparator19";
            resources.ApplyResources(toolStripSeparator19, "toolStripSeparator19");
            // 
            // toolStripSeparator21
            // 
            toolStripSeparator21.Name = "toolStripSeparator21";
            resources.ApplyResources(toolStripSeparator21, "toolStripSeparator21");
            // 
            // toolStripSeparator20
            // 
            toolStripSeparator20.Name = "toolStripSeparator20";
            resources.ApplyResources(toolStripSeparator20, "toolStripSeparator20");
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            resources.ApplyResources(toolStripSeparator1, "toolStripSeparator1");
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            resources.ApplyResources(this.toolStripSeparator2, "toolStripSeparator2");
            // 
            // toolStripSeparator7
            // 
            this.toolStripSeparator7.Name = "toolStripSeparator7";
            resources.ApplyResources(this.toolStripSeparator7, "toolStripSeparator7");
            // 
            // toolStripSeparator
            // 
            this.toolStripSeparator.Name = "toolStripSeparator";
            resources.ApplyResources(this.toolStripSeparator, "toolStripSeparator");
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            resources.ApplyResources(this.toolStripSeparator3, "toolStripSeparator3");
            // 
            // toolStripSeparator8
            // 
            this.toolStripSeparator8.Name = "toolStripSeparator8";
            resources.ApplyResources(this.toolStripSeparator8, "toolStripSeparator8");
            // 
            // toolStripSeparator10
            // 
            this.toolStripSeparator10.Name = "toolStripSeparator10";
            resources.ApplyResources(this.toolStripSeparator10, "toolStripSeparator10");
            // 
            // toolStripSeparator11
            // 
            this.toolStripSeparator11.Name = "toolStripSeparator11";
            resources.ApplyResources(this.toolStripSeparator11, "toolStripSeparator11");
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            resources.ApplyResources(this.toolStripMenuItem1, "toolStripMenuItem1");
            // 
            // toolStripStatusLabel1
            // 
            this.toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            resources.ApplyResources(this.toolStripStatusLabel1, "toolStripStatusLabel1");
            // 
            // toolStripButton1
            // 
            this.toolStripButton1.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            resources.ApplyResources(this.toolStripButton1, "toolStripButton1");
            this.toolStripButton1.Name = "toolStripButton1";
            // 
            // saveFileDialog
            // 
            this.saveFileDialog.FileName = "hosts";
            resources.ApplyResources(this.saveFileDialog, "saveFileDialog");
            this.saveFileDialog.InitialDirectory = "C:\\Windows\\System32\\drivers\\etc";
            // 
            // notifyIcon
            // 
            this.notifyIcon.ContextMenuStrip = this.contextMenuTray;
            resources.ApplyResources(this.notifyIcon, "notifyIcon");
            this.notifyIcon.DoubleClick += new System.EventHandler(this.OnNotifyIconDoubleClick);
            // 
            // contextMenuTray
            // 
            this.contextMenuTray.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.menuContextEdit,
            toolStripSeparator21,
            this.menuContextDisable,
            toolStripSeparator20,
            this.contextMenuExit});
            this.contextMenuTray.Name = "contextMenuTray";
            resources.ApplyResources(this.contextMenuTray, "contextMenuTray");
            // 
            // menuContextEdit
            // 
            this.menuContextEdit.Name = "menuContextEdit";
            resources.ApplyResources(this.menuContextEdit, "menuContextEdit");
            this.menuContextEdit.Click += new System.EventHandler(this.OnEditClick);
            // 
            // menuContextDisable
            // 
            this.menuContextDisable.Image = global::HostsFileEditor.Properties.Resources.Disable;
            this.menuContextDisable.Name = "menuContextDisable";
            resources.ApplyResources(this.menuContextDisable, "menuContextDisable");
            this.menuContextDisable.Click += new System.EventHandler(this.OnDisableHostsClick);
            // 
            // contextMenuExit
            // 
            this.contextMenuExit.Name = "contextMenuExit";
            resources.ApplyResources(this.contextMenuExit, "contextMenuExit");
            this.contextMenuExit.Click += new System.EventHandler(this.OnExitClick);
            // 
            // openFileDialog
            // 
            this.openFileDialog.FileName = "hosts";
            resources.ApplyResources(this.openFileDialog, "openFileDialog");
            // 
            // bindingSourceHostEntries
            // 
            this.bindingSourceHostEntries.DataSource = typeof(HostsFileEditor.HostsEntryList);
            // 
            // helpToolStripMenuItem
            // 
            this.helpToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.aboutToolStripMenuItem});
            this.helpToolStripMenuItem.Name = "helpToolStripMenuItem";
            resources.ApplyResources(this.helpToolStripMenuItem, "helpToolStripMenuItem");
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            resources.ApplyResources(this.aboutToolStripMenuItem, "aboutToolStripMenuItem");
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.OnAboutClick);
            // 
            // MainForm
            // 
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.toolStripContainer);
            this.MainMenuStrip = this.menuStrip;
            this.Name = "MainForm";
            this.ShowInTaskbar = false;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.OnFormClosing);
            this.Load += new System.EventHandler(this.OnFomLoad);
            this.Shown += new System.EventHandler(this.OnFormShown);
            this.ResizeEnd += new System.EventHandler(this.OnResizingEnd);
            this.VisibleChanged += new System.EventHandler(this.OnVisibleChanged);
            this.toolStripContainer.BottomToolStripPanel.ResumeLayout(false);
            this.toolStripContainer.BottomToolStripPanel.PerformLayout();
            this.toolStripContainer.ContentPanel.ResumeLayout(false);
            this.toolStripContainer.TopToolStripPanel.ResumeLayout(false);
            this.toolStripContainer.TopToolStripPanel.PerformLayout();
            this.toolStripContainer.ResumeLayout(false);
            this.toolStripContainer.PerformLayout();
            this.statusStrip.ResumeLayout(false);
            this.statusStrip.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceHostFile)).EndInit();
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.Panel2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitContainer)).EndInit();
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewHostsEntries)).EndInit();
            this.contextMenuGrid.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceView)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewArchive)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceArchive)).EndInit();
            this.toolStripArchive.ResumeLayout(false);
            this.toolStripArchive.PerformLayout();
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.toolStrip.ResumeLayout(false);
            this.toolStrip.PerformLayout();
            this.contextMenuTray.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceHostEntries)).EndInit();
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.ToolStripContainer toolStripContainer;
        private HostsFileEditor.Controls.MenuStripEx menuStrip;
        private System.Windows.Forms.ToolStripMenuItem menuFile;
        private System.Windows.Forms.ToolStripMenuItem menuSave;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator;
        private System.Windows.Forms.ToolStripMenuItem menuExit;
        private HostsFileEditor.Controls.HostsEntryDataGridView dataGridViewHostsEntries;
        private System.Windows.Forms.BindingSource bindingSourceView;
        private System.Windows.Forms.ToolStripMenuItem menuRestoreDefaults;
        private HostsFileEditor.Controls.ToolStripEx toolStrip;
        private System.Windows.Forms.ToolStripButton buttonSave;
        private System.Windows.Forms.ToolStripButton buttonDisable;
        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel labelHostEntries;
        private System.Windows.Forms.ToolStripMenuItem menuSaveAs;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem menuDisable;
        private System.Windows.Forms.ToolStripMenuItem menuImport;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem editToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem menuUndo;
        private System.Windows.Forms.ToolStripMenuItem menuRedo;
        private System.Windows.Forms.ToolStripMenuItem menuCut;
        private System.Windows.Forms.ToolStripMenuItem menuCopy;
        private System.Windows.Forms.ToolStripMenuItem menuPaste;
        private System.Windows.Forms.ToolStripMenuItem menuDelete;
        private System.Windows.Forms.ToolStripMenuItem menuMoveUp;
        private System.Windows.Forms.ToolStripMenuItem menuMoveDown;
        private System.Windows.Forms.ToolStripMenuItem viewToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem menuViewArchive;
        private System.Windows.Forms.ToolStripMenuItem menuArchive;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator7;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator8;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator10;
        private System.Windows.Forms.ToolStripMenuItem menuFilterComments;
        private System.Windows.Forms.ToolStripMenuItem menuFilterDisabled;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator11;
        private HostsFileEditor.Controls.ToolStripSpringTextBox textFilter;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItem1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabel1;
        private System.Windows.Forms.ToolStripButton toolStripButton1;
        private System.Windows.Forms.SaveFileDialog saveFileDialog;
        private HostsFileEditor.Controls.ToolStripBindableStatusLabel labelHostEntriesCount;
        private System.Windows.Forms.BindingSource bindingSourceHostEntries;
        private System.Windows.Forms.ToolStripLabel toolStripDropDownButton1;
        private System.Windows.Forms.ToolStripButton buttonFilterComment;
        private System.Windows.Forms.ToolStripButton buttonFilterDisabled;
        private System.Windows.Forms.DataGridViewCheckBoxColumn columnValid;
        private System.Windows.Forms.DataGridViewCheckBoxColumn columnEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnIpAddress;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnHostnames;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnComment;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnUnparsedText;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnFiller;
        private System.Windows.Forms.NotifyIcon notifyIcon;
        private System.Windows.Forms.ContextMenuStrip contextMenuTray;
        private System.Windows.Forms.ToolStripMenuItem menuContextDisable;
        private System.Windows.Forms.ToolStripMenuItem contextMenuExit;
        private System.Windows.Forms.ToolStripMenuItem menuContextEdit;
        private System.Windows.Forms.ToolStripStatusLabel labelLineCount;
        private HostsFileEditor.Controls.ToolStripBindableStatusLabel labelLineCountNumber;
        private System.Windows.Forms.BindingSource bindingSourceHostFile;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.ToolStripMenuItem insertRowAboveToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem insertRowBelowToolStripMenuItem;
        private System.Windows.Forms.ContextMenuStrip contextMenuGrid;
        private System.Windows.Forms.ToolStripMenuItem menuContextCut;
        private System.Windows.Forms.ToolStripMenuItem menuContextCopy;
        private System.Windows.Forms.ToolStripMenuItem menuContextPaste;
        private System.Windows.Forms.ToolStripMenuItem menuContextDelete;
        private System.Windows.Forms.ToolStripMenuItem menuContextMoveUp;
        private System.Windows.Forms.ToolStripMenuItem menuContextMoveDown;
        private System.Windows.Forms.ToolStripMenuItem menuContextInsertAbove;
        private System.Windows.Forms.ToolStripMenuItem menuContextInsertBelow;
        private System.Windows.Forms.ToolStripMenuItem menuRefresh;
        private System.Windows.Forms.ToolStripButton buttonRefresh;
        private System.Windows.Forms.ToolStripMenuItem menuTools;
        private System.Windows.Forms.ToolStripMenuItem menuPingIPs;
        private System.Windows.Forms.ToolStripButton buttonViewArchive;
        private System.Windows.Forms.SplitContainer splitContainer;
        private HostsFileEditor.Controls.ToolStripEx toolStripArchive;
        private System.Windows.Forms.ToolStripButton buttonDeleteArchive;
        private HostsFileEditor.Controls.HostsArchiveDataGridView dataGridViewArchive;
        private System.Windows.Forms.ToolStripButton buttonLoadArchive;
        private System.Windows.Forms.DataGridViewTextBoxColumn fileNameDataGridViewTextBoxColumn;
        private System.Windows.Forms.BindingSource bindingSourceArchive;
        private System.Windows.Forms.ToolStripButton buttonArchive;
        private System.Windows.Forms.ToolStripMenuItem menuRemoveDefaultText;

        #endregion
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator29;
        private System.Windows.Forms.ToolStripMenuItem menuRemoveSort;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator30;
        private System.Windows.Forms.ToolStripMenuItem menuCheck;
        private System.Windows.Forms.ToolStripMenuItem menuUncheck;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator31;
        private System.Windows.Forms.ToolStripMenuItem contextMenuCheck;
        private System.Windows.Forms.ToolStripMenuItem contextMenuUncheck;
        private System.Windows.Forms.ToolStripMenuItem helpToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem openTextEditor;
    }
}

