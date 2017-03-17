// <copyright file="NativeMethods.cs" company="N/A">
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

namespace HostsFileEditor.Win32
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;

    /// <summary>
    /// The native methods.
    /// </summary>
    internal static class NativeMethods
    {
        /// <summary>
        /// The broadcast handle.
        /// </summary>
        public const int HWND_BROADCAST = 0xffff;

        /// <summary>
        /// Registers the window message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Error code.</returns>
        [DllImport("user32", CharSet = CharSet.Unicode)]
        public static extern int RegisterWindowMessage(string message);

        /// <summary>
        /// Sends the message.
        /// </summary>
        /// <param name="hWnd">The HWND.</param>
        /// <param name="Msg">The MSG.</param>
        /// <param name="wParam">The wparam.</param>
        /// <param name="lParam">The lparam.</param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

        /// <summary>
        /// Registers the window message.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        public static int RegisterWindowMessage(string format, params object[] args)
        {
            string message = String.Format(format, args);
            return RegisterWindowMessage(message);
        }

        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
        private static extern uint DnsFlushResolverCache();

        /// <summary>
        /// Flush DNS resolver cache. This is best effort as underlying error codes are ignored.
        /// </summary>
        public static void FlushDns()
        {
            var result = DnsFlushResolverCache();
            Debug.WriteLine($"DnsFlushResolverCache: {result}");
        }
    }
}
