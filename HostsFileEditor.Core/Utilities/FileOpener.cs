using Microsoft.Win32;
using System.ComponentModel;
using System.Diagnostics;

namespace HostsFileEditor.Utilities;

public static class FileOpener
{
    /// <summary>ERROR_CANCELLED — returned by ShellExecute when the user declines the UAC prompt.</summary>
    private const int ErrorCancelled = 1223;

    public static void OpenTextFile(string path)
    {
        if (!TryGetRegisteredApplication(".txt", out var application) || application == null)
        {
            application = "notepad.exe";
        }

        // The hosts file is admin-owned and this app runs as a standard user (asInvoker), so an
        // editor launched with the default token cannot save edits to it. Launch the editor
        // elevated via the "runas" verb (a UAC prompt) so it can actually write the file. A
        // declined prompt (ERROR_CANCELLED) just means "don't open" — swallow it, don't crash.
        var startInfo = new ProcessStartInfo
        {
            FileName = application,
            Arguments = $"\"{path}\"",
            UseShellExecute = true,
            Verb = "runas",
        };

        try
        {
            Process.Start(startInfo);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // User declined the elevation prompt; nothing to open.
        }
    }

    private static bool TryGetRegisteredApplication(string extension, out string? registeredApp)
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
            .Replace("%1", string.Empty, StringComparison.Ordinal)
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .Trim();

        return true;
    }

    private static string? GetClassesRootKeyDefaultValue(string keyPath)
    {
        using var key = Registry.ClassesRoot.OpenSubKey(keyPath);
        return key?.GetValue(null)?.ToString();
    }
}
