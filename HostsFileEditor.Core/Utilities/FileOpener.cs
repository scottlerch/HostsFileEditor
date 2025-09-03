using Microsoft.Win32;
using System.Diagnostics;

namespace HostsFileEditor.Utilities;

public class FileOpener
{
    public static void OpenTextFile(string path)
    {
        if (!TryGetRegisteredApplication(".txt", out var application) || application == null)
        {
            application = "notepad.exe";
        }

        Process.Start(application, path);
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
            .Replace("%1", string.Empty)
            .Replace("\"", string.Empty)
            .Trim();

        return true;
    }

    private static string? GetClassesRootKeyDefaultValue(string keyPath)
    {
        using RegistryKey? key = Registry.ClassesRoot.OpenSubKey(keyPath);
        return key?.GetValue(null)?.ToString();
    }
}
