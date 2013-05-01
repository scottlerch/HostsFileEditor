// <copyright file="HostsArchiveList.cs" company="N/A">
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
    using System.ComponentModel;
    using System.IO;
    using HostsFileEditor.Extensions;
    using HostsFileEditor.Utilities;

    /// <summary>
    /// Hosts mod list.
    /// </summary>
    internal class HostsModList : BindingList<HostsMod>
    {
        /// <summary>
        /// The mod directory.
        /// </summary>
        public static readonly string ModDirectory = 
            Path.Combine(HostsFile.DefaultHostFileDirectory, "mod");

        /// <summary>
        /// Singleton instance.
        /// </summary>
        private static readonly Lazy<HostsModList> instance = 
            new Lazy<HostsModList>(() => new HostsModList());

        /// <summary>
        /// Prevents a default instance of the <see cref="HostsModList"/> class from being created.
        /// </summary>
        private HostsModList()
        {
            this.Refresh();
        }

        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static HostsModList Instance 
        { 
            get { return instance.Value; } 
        }

        /// <summary>
        /// Deletes the specified mod.
        /// </summary>
        /// <param name="mod">The mod.</param>
        public void Delete(HostsMod mod)
        {
            using (FileEx.DisableAttributes(mod.FilePath, FileAttributes.ReadOnly))
            {
                File.Delete(mod.FilePath);
            }

            this.Remove(mod);
        }

        /// <summary>
        /// Refreshes this instance.
        /// </summary>
        public void Refresh()
        {
            this.BatchUpdate(() =>
            {
                this.Clear();

                if (Directory.Exists(ModDirectory))
                {
                    var files = Directory.GetFiles(ModDirectory);

                    foreach (var file in files)
                    {
                        this.Add(new HostsMod { FilePath = file });
                    }
                }
            });
        }
    }
}
