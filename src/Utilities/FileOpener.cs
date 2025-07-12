// <copyright file="FileEx.cs" company="N/A">
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

using Microsoft.Win32;
using System.Diagnostics;

namespace HostsFileEditor.Utilities;

/// <summary>
/// Helper class to open files.
/// </summary>
public class FileOpener
{
    /// <summary>
    /// Open text file using default text editor.
    /// </summary>
    /// <param name="path">Full path to text file.</param>
    public static void OpenTextFile(string path)
    {
        if (!TryGetRegisteredApplication(".txt", out string application))
        {
            application = "notepad.exe";
        }

        Process.Start(application, path);
    }

    private static bool TryGetRegisteredApplication(string extension, out string registeredApp)
    {
        var extensionId = GetClassesRootKeyDefaultValue(extension);

        if (extensionId == null)
        {
            registeredApp = null;
            return false;
        }

        var openCommand = GetClassesRootKeyDefaultValue(extensionId + @"\shell\open\command");

        if (openCommand == null)
        {
            registeredApp = null;
            return false;
        }

        registeredApp = openCommand
            .Replace("%1", string.Empty)
            .Replace("\"", string.Empty)
            .Trim();

        return true;
    }

    private static string GetClassesRootKeyDefaultValue(string keyPath)
    {
        using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
        return key?.GetValue(null)?.ToString();
    }
}
