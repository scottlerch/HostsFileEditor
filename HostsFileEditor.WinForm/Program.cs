using HostsFileEditor.CommandLine;
using HostsFileEditor.Properties;

namespace HostsFileEditor;

/// <summary>
/// The program class containing the main entry point.
/// </summary>
internal static class Program
{
    /// <summary>
    /// The application's main form.
    /// </summary>
    private static Form? _mainForm;

    /// <summary>
    /// The main entry point for the application.
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        // Headless command line (issue #2): any argument that isn't the Jump List's GUI-launch switch
        // (--open-archive) is treated as a command — run it against the launching console and exit with
        // a status code, never showing a window (an unknown command reports an error rather than opening
        // the GUI). No args, or --open-archive, falls through to the normal windowed startup.
        if (args.Length > 0 &&
            !string.Equals(args[0], TaskbarJumpList.OpenArchiveSwitch, StringComparison.OrdinalIgnoreCase))
        {
            ConsoleAttach.AttachToParentConsole();
            return HostsCli.Run(args, Console.Out, Console.Error);
        }

        // Ensure only one copy of the application is running at a time
        using var program = ProgramSingleInstance.Start();
        if (program.IsOnlyInstance)
        {
            // Applies visual styles, GDI text rendering, and the high-DPI mode
            // (PerMonitorV2) configured via the Application* MSBuild properties in the csproj.
            ApplicationConfiguration.Initialize();
            Application.ThreadException += OnApplicationThreadException;

            // Run as a standard user (asInvoker) and elevate only the privileged hosts-file
            // operations on demand via the bundled helper. Required for the Microsoft Store.
            Elevation.PrivilegedFileOperations.UseElevationHelper();

            _mainForm = new MainForm();
            Application.Run(_mainForm);
        }
        else
        {
            // A Jump List "open preset" launched this second instance (issue #10). Hand the archive
            // path to the already-running instance (which reads it when the show-broadcast arrives),
            // then bring it to the foreground and exit.
            var openArchivePath = TaskbarJumpList.TryGetOpenArchivePath(Environment.GetCommandLineArgs());
            if (openArchivePath is not null)
            {
                TaskbarJumpList.WritePendingOpenArchive(openArchivePath);
            }

            ProgramSingleInstance.ShowFirstInstance();
        }

        return 0;
    }

    /// <summary>
    /// The on application thread exception.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private static void OnApplicationThreadException(object sender, ThreadExceptionEventArgs e)
    {
        MessageBox.Show(
            null,
            e.Exception.Message,
            Resources.ErrorCaption,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error,
            MessageBoxDefaultButton.Button1,
            MessageBoxOptions.DefaultDesktopOnly);
    }
}
