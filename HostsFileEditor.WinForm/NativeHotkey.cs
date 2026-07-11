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

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
