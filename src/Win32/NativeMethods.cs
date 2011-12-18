// -----------------------------------------------------------------------
// <copyright file="NativeMethods.cs" company="">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace HostsFileEditor.Win32
{
    using System;
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
        /// Show window normal setting.
        /// </summary>
        public const int SW_SHOWNORMAL = 1;

        /// <summary>
        /// Registers the window message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>Error code.</returns>
        [DllImport("user32")]
        public static extern int RegisterWindowMessage(string message);

        /// <summary>
        /// The set foreground window.
        /// </summary>
        /// <param name="hWnd">
        /// The hardware instance.
        /// </param>
        /// <returns>
        /// True is successful, false otherwise.
        /// </returns>
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        /// <summary>
        /// Shows the window.
        /// </summary>
        /// <param name="hWnd">The window instance.</param>
        /// <param name="nCmdShow">The show command.</param>
        /// <returns></returns>
        [DllImportAttribute("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        /// <summary>
        /// Posts the message.
        /// </summary>
        /// <param name="hwnd">The HWND.</param>
        /// <param name="msg">The MSG.</param>
        /// <param name="wparam">The wparam.</param>
        /// <param name="lparam">The lparam.</param>
        /// <returns></returns>
        [DllImport("user32")]
        public static extern bool PostMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

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

        /// <summary>
        /// Show window to front.
        /// </summary>
        /// <param name="window">The window.</param>
        public static void ShowToFront(IntPtr window)
        {
            ShowWindow(window, SW_SHOWNORMAL);
            SetForegroundWindow(window);
        }
    }
}
