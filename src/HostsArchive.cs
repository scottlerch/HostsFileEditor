// <copyright file="HostsArchive.cs" company="N/A">
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
    using System.IO;
    using System.Linq;
    using HostsFileEditor.Extensions;
    using HostsFileEditor.Properties;

    /// <summary>
    /// Hosts archive.
    /// </summary>
    internal class HostsArchive
    {
        /// <summary>
        /// The full file path.
        /// </summary>
        private string filePath;

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsArchive"/> class.
        /// </summary>
        public HostsArchive()
        {
            this.FilePath = string.Empty;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="HostsArchive"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <exception cref="ArgumentNullException">
        /// name cannot be null
        /// </exception>
        public HostsArchive(string name)
        {
            name.ThrowIfNull("name");

            this.FilePath = Path.Combine(HostsArchiveList.ArchiveDirectory, name);
        }

        /// <summary>
        /// Gets or sets the file path.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// value cannot be null
        /// </exception>
        public string FilePath 
        {
            get
            {
                return this.filePath;
            }

            set
            {
                value.ThrowIfNull("value");

                this.filePath = value;
            }
        }

        /// <summary>
        /// Gets the name of the file.
        /// </summary>
        public string FileName
        { 
            get
            {
                return this.FilePath
                    .Split(Path.DirectorySeparatorChar)
                    .LastOrDefault();
            }
        }

        /// <summary>
        /// Validates the specified file path.
        /// </summary>
        /// <param name="filePath">The file path.</param>
        /// <param name="error">
        /// The error.  This will be the emptry string if there is no error.
        /// </param>
        /// <returns>true if file path is valid for archive, false otherwise</returns>
        public static bool Validate(string filePath, out string error)
        {
            bool isValid = false;

            error = string.Empty;

            /* TODO: Is there a better way to determine file name is valid
             * instead of catching exception from FileInfo?
             */

            try
            {
                new FileInfo(filePath);
                isValid = true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            if (isValid)
            {
                if (Directory.Exists(HostsArchiveList.ArchiveDirectory))
                {
                    if (Directory.GetFiles(HostsArchiveList.ArchiveDirectory)
                        .Select(fullFilePath => Path.GetFileName(fullFilePath))
                        .Contains(filePath))
                    {
                        isValid = false;
                        error = Resources.ArchiveExists;
                    }
                }
            }

            return isValid;
        }
    }
}
