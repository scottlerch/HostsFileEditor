using System.Runtime.InteropServices;

namespace HostsFileEditor;

/// <summary>
/// Thin P/Invoke wrapper over the Win32 global hot-key API (issue #35): a hot key registered here
/// fires <see cref="WmHotkey"/> to the owning window's message loop even when the app has no focus,
/// which is what lets a shortcut restore the window from the tray.
/// </summary>
internal static partial class NativeHotkey
{
    /// <summary>WM_HOTKEY — posted to the registering window when its hot key is pressed.</summary>
    public const int WmHotkey = 0x0312;

    // fsModifiers flags for RegisterHotKey.
    public const uint ModAlt = 0x0001;
    public const uint ModControl = 0x0002;
    public const uint ModShift = 0x0004;

    /// <summary>
    /// MOD_NOREPEAT — coalesce keyboard auto-repeat so holding the combo fires WM_HOTKEY once per
    /// physical press instead of a stream that flickers the window (Windows 7+).
    /// </summary>
    public const uint ModNoRepeat = 0x4000;

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    /// <summary>Foreground window HWND — lets the toggle hide only when the window is already in front.</summary>
    [LibraryImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial IntPtr GetForegroundWindow();
}
