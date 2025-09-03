// <copyright file="HostsArchiveList.cs" company="N/A">
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

namespace HostsFileEditor;

using HostsFileEditor.Extensions;
using HostsFileEditor.Utilities;
using System;
using System.ComponentModel;
using System.IO;

/// <summary>
/// Hosts archive list.
/// </summary>
internal class HostsArchiveList : BindingList<HostsArchive>
{
    /// <summary>
    /// The archive directory.
    /// </summary>
    public static readonly string ArchiveDirectory = 
        Path.Combine(HostsFile.DefaultHostFileDirectory, "archive");

    /// <summary>
    /// Singleton instance.
    /// </summary>
    private static readonly Lazy<HostsArchiveList> instance = 
        new(() => new HostsArchiveList());

    /// <summary>
    /// Prevents a default instance of the <see cref="HostsArchiveList"/> class from being created.
    /// </summary>
    private HostsArchiveList()
    {
        Refresh();
    }

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static HostsArchiveList Instance => instance.Value;

    /// <summary>
    /// Deletes the specified archive.
    /// </summary>
    /// <param name="archive">The archive.</param>
    public void Delete(HostsArchive archive)
    {
        using (FileEx.DisableAttributes(archive.FilePath, FileAttributes.ReadOnly))
        {
            File.Delete(archive.FilePath);
        }

        Remove(archive);
    }

    /// <summary>
    /// Refreshes this instance.
    /// </summary>
    public void Refresh()
    {
        this.BatchUpdate(() =>
        {
            Clear();

            if (Directory.Exists(ArchiveDirectory))
            {
                var files = Directory.GetFiles(ArchiveDirectory);

                foreach (var file in files)
                {
                    Add(new HostsArchive { FilePath = file });
                }
            }
        });
    }
}
