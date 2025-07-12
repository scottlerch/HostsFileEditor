// <copyright file="SingleInstance.cs" company="N/A">
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

using HostsFileEditor.Win32;
using System.Diagnostics;

namespace HostsFileEditor;

/// <summary>
/// Helper class to enforce single instance of a program.
/// http://www.codeproject.com/KB/cs/SingleInstanceAppMutex.aspx
/// </summary>
public sealed class ProgramSingleInstance : IDisposable
{
    /// <summary>
    /// Message to tell first instance to show itself.
    /// </summary>
    public static readonly int WM_SHOWFIRSTINSTANCE =
        NativeMethods.RegisterWindowMessage(
            "WM_SHOWFIRSTINSTANCE|{0}", 
            ProgramInfo.AssemblyGuid);

    /// <summary>
    /// The program mutex.
    /// </summary>
    private Mutex mutex;

    /// <summary>
    /// Gets a value indicating whether this instance is only instance.
    /// </summary>
    /// <value>
    /// <c>true</c> if this instance is only instance; otherwise, <c>false</c>.
    /// </value>
    public bool IsOnlyInstance { get; private set; }

    private ProgramSingleInstance()
    {
        IsOnlyInstance = false;
        string mutexName = $"Local\\{ProgramInfo.AssemblyGuid}";

        // if you want your app to be limited to a single instance
        // across ALL SESSIONS (multiple users & terminal services), then use the following line instead:
        // string mutexName = $"Global\\{ProgramInfo.AssemblyGuid}";

        mutex = new Mutex(true, mutexName, out bool onlyInstance);
        IsOnlyInstance = onlyInstance;
    }

    /// <summary>
    /// Starts this instance.
    /// </summary>
    /// <returns>True if successful.</returns>
    public static ProgramSingleInstance Start()
    {
        return new ProgramSingleInstance();
    }

    /// <summary>
    /// Shows the first instance.
    /// </summary>
    public static void ShowFirstInstance()
    {
        // HACK: the second process won't return from SendMessage
        // so kill process after a few seconds
        Task.Factory.StartNew(async () => 
        {
            await Task.Delay(5000);
            Process.GetCurrentProcess().Kill();
        });

        // Must use SendMessage instead of PostMessage so the
        // first instance can still receive the message even
        // if it's minimized to tray bar
        NativeMethods.SendMessage(
            (IntPtr)NativeMethods.HWND_BROADCAST,
            WM_SHOWFIRSTINSTANCE,
            IntPtr.Zero,
            IntPtr.Zero);
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        if (mutex != null)
        {
            if (IsOnlyInstance)
            {
                mutex.ReleaseMutex();
            }

            mutex.Dispose();
            mutex = null;
        }
    }
}
