// <copyright file="HostsEntryDataGridView.cs" company="N/A">
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

namespace HostsFileEditor.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Windows.Forms;
    using Equin.ApplicationFramework;

    /// <summary>
    /// DataGridView class for HostsEntry.
    /// </summary>
    internal sealed class HostsEntryDataGridView : DataGridView
    {
        #region Fields and Constants

        /// <summary>
        /// Current sort state used to determine when to remove sort.
        /// </summary>
        private int currentSortState = 0;

        /// <summary>
        /// Last sorted column.
        /// </summary>
        private DataGridViewColumn lastSortedColumn = null;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsEntryDataGridView"/> class.
        /// </summary>
        public HostsEntryDataGridView()
        {
            this.AllowUserToResizeRows = false;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the action to clear the sort of the underlying 
        /// data source.
        /// </summary>
        public Action ClearSort
        {
            get;
            set;
        }

        /// <summary>
        /// Gets the selected host entries.
        /// </summary>
        public IEnumerable<HostsEntry> SelectedHostEntries
        {
            get 
            { 
                return this.SelectedRows
                    .Cast<DataGridViewRow>()
                    .Select(row => row.DataBoundItem as ObjectView<HostsEntry>)
                    .Where(view => view != null && view.Object != null)
                    .Select(view => view.Object); 
            }
        }

        /// <summary>
        /// Gets the current host entry.
        /// </summary>
        public HostsEntry CurrentHostEntry
        {
            get
            {
                var view = this.CurrentRow.DataBoundItem as ObjectView<HostsEntry>;
                if (view != null)
                {
                    return view.Object;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Gets the last selected host entry.
        /// </summary>
        public HostsEntry LastSelectedHostEntry
        {
            get
            {
                return this.SelectedHostEntries.LastOrDefault();
            }
        }

        /// <summary>
        /// Gets the first selected host entry.
        /// </summary>
        public HostsEntry FirstSelectedHostEntry
        {
            get
            {
                return this.SelectedHostEntries.FirstOrDefault();
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.DataGridView.CurrentCellDirtyStateChanged"/> event.
        /// </summary>
        /// <param name="e">An <see cref="T:System.EventArgs"/> that contains the event data.</param>
        protected override void OnCurrentCellDirtyStateChanged(System.EventArgs e)
        {
            base.OnCurrentCellDirtyStateChanged(e);

            // Immediately commit check changes
            if (this.CurrentCell.GetType() == typeof(DataGridViewCheckBoxCell))
            {
                this.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.DataGridView.CellFormatting"/> event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The value of the 
        /// <see cref="P:System.Windows.Forms.DataGridViewCellFormattingEventArgs.ColumnIndex"/> 
        /// property of <paramref name="e"/> is greater than the number of
        /// columns in the control minus one.-or-The value of the 
        /// <see cref="P:System.Windows.Forms.DataGridViewCellFormattingEventArgs.RowIndex"/>
        /// property of <paramref name="e"/> is greater than the number of
        /// rows in the control minus one.</exception>
        protected override void OnCellFormatting(DataGridViewCellFormattingEventArgs e)
        {
            var viewObject = this.Rows[e.RowIndex].DataBoundItem as ObjectView<HostsEntry>;
            HostsEntry entry = viewObject != null ? viewObject.Object : null;

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
                else if (!entry.Valid)
                {
                    e.CellStyle.BackColor = Color.LightPink;
                }
                else
                {
                    e.CellStyle.BackColor = Color.White;
                }
            }

            base.OnCellFormatting(e);
        }

        /// <summary>
        /// Raises the <see cref="E:System.Windows.Forms.DataGridView.ColumnHeaderMouseClick"/> 
        /// event.
        /// </summary>
        /// <param name="e">A 
        /// <see cref="T:System.Windows.Forms.DataGridViewCellMouseEventArgs"/> 
        /// that contains the event data.</param>
        /// <exception cref="T:System.ArgumentOutOfRangeException">The value of 
        /// the <see cref="P:System.Windows.Forms.DataGridViewCellMouseEventArgs.ColumnIndex"/> 
        /// property of <paramref name="e"/> is less than zero or greater than 
        /// the number of columns in the control minus one.</exception>
        protected override void OnColumnHeaderMouseClick(DataGridViewCellMouseEventArgs e)
        {
            base.OnColumnHeaderMouseClick(e);

            if (this.SortedColumn == lastSortedColumn)
            {
                this.currentSortState++;

                // After sorting twice (ascending then descending) clear the sort
                if (this.currentSortState > 2)
                {
                    this.BeginInvoke(
                        (MethodInvoker)delegate() 
                        { 
                            Application.DoEvents();
                            if (this.ClearSort != null)
                            {
                                this.ClearSort();
                            }
                        });
                    
                    this.currentSortState = 0;
                    this.lastSortedColumn = null;
                }
            }
            else
            {
                this.currentSortState = 1;
                this.lastSortedColumn = this.Columns[e.ColumnIndex];
            }
        }

        #endregion
    }
}