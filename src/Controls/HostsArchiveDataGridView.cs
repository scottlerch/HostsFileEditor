// <copyright file="HostsArchiveDataGridView.cs" company="N/A">
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
    public HostsArchive CurrentHostsArchive
    {
        get
        {
            HostsArchive archive = null;

            if (CurrentRow != null)
            {
                var view = CurrentRow.DataBoundItem as ObjectView<HostsArchive>;
                archive = view.Object;
            }

            return archive;
        }
    }
}