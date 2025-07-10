// <copyright file="ProgramInfo.cs" company="N/A">
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

namespace HostsFileEditor
{
    using System;
    using System.Reflection;
    using System.Runtime.InteropServices;

    /// <summary>
    /// Program information.
    /// </summary>
    static public class ProgramInfo
    {
        /// <summary>
        /// Gets the assembly GUID.
        /// </summary>
        static public string AssemblyGuid
        {
            get
            {
                object[] attributes = Assembly
                    .GetEntryAssembly()
                    .GetCustomAttributes(typeof(GuidAttribute), false);

                if (attributes.Length == 0)
                {
                    return String.Empty;
                }

                return ((GuidAttribute)attributes[0]).Value;
            }
        }
    } 
}
