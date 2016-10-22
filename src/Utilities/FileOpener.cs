using Microsoft.Win32;
using System.Diagnostics;

namespace HostsFileEditor.Utilities
{
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
            string application;
            if (!TryGetRegisteredApplication(".txt", out application))
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

            var openCommand = GetClassesRootKeyDefaultValue(extensionId + "/shell/open/command");

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
            using (var key = Registry.ClassesRoot.OpenSubKey(keyPath))
            {
                return key?.GetValue(null)?.ToString();
            }
        }
    }
}
