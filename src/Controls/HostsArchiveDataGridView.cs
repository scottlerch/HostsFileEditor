// <copyright file="HostsArchiveDataGridView.cs" company="N/A">
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
    using System.Windows.Forms;
    using Equin.ApplicationFramework;

    /// <summary>
    /// DataGridView class for use with HostsArchive objects.
    /// </summary>
    internal sealed class HostsArchiveDataGridView : DataGridView
    {
        #region Constructors and Destructors

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsArchiveDataGridView"/> class.
        /// </summary>
        public HostsArchiveDataGridView()
        {
            this.AllowUserToResizeRows = false;
            this.AllowUserToResizeColumns = false;
            this.AllowUserToDeleteRows = false;
            this.AllowDrop = false;
            this.AllowUserToAddRows = false;
            this.AllowUserToOrderColumns = false;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets the current hosts archive.
        /// </summary>
        public HostsArchive CurrentHostsArchive
        {
            get
            {
                HostsArchive archive = null;

                if (this.CurrentRow != null)
                {
                    var view = this.CurrentRow.DataBoundItem as ObjectView<HostsArchive>;
                    archive = view.Object;
                }

                return archive;
            }
        }

        #endregion
    }
}