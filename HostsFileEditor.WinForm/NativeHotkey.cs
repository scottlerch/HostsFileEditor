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
    public const uint ModWin = 0x0008;

    /// <summary>
    /// MOD_NOREPEAT — coalesce keyboard auto-repeat so holding the combo fires WM_HOTKEY once per
    /// physical press instead of a stream that flickers the window (Windows 7+).
    /// </summary>
    public const uint ModNoRepeat = 0x4000;

    /// <summary>
    /// Parses a human-friendly hot-key string (issue #35) like <c>"Control, Shift, H"</c> or
    /// <c>"Ctrl+Alt+F8"</c> into <see cref="RegisterHotKey"/>'s <paramref name="modifiers"/> +
    /// <paramref name="key"/>. Tokens are split on <c>+</c> or <c>,</c>, case-insensitive: Ctrl/Control,
    /// Alt, Shift, Win/Windows are modifiers; the single remaining token is a <see cref="Keys"/> name.
    /// Requires at least one modifier AND exactly one key (a bare key would grab that key system-wide),
    /// so an empty/blank/malformed value returns <see langword="false"/> — the caller treats that as
    /// "disabled". MOD_NOREPEAT is NOT included; the caller adds it.
    /// </summary>
    public static bool TryParseHotkey(string? text, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        foreach (var token in text.Split(['+', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (token.ToLowerInvariant())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModControl;
                    break;
                case "alt":
                    modifiers |= ModAlt;
                    break;
                case "shift":
                    modifiers |= ModShift;
                    break;
                case "win":
                case "windows":
                    modifiers |= ModWin;
                    break;
                default:
                    // Exactly one non-modifier token, and it must be a real key.
                    if (key != 0 || !Enum.TryParse<Keys>(token, ignoreCase: true, out var parsed) || parsed == Keys.None)
                    {
                        return false;
                    }

                    key = (uint)parsed;
                    break;
            }
        }

        return modifiers != 0 && key != 0;
    }

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
