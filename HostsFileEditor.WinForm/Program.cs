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
    private static void Main()
    {
        // Ensure only one copy of the application is running at a time
        using var program = ProgramSingleInstance.Start();
        if (program.IsOnlyInstance)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += OnApplicationThreadException;

            // Run as a standard user (asInvoker) and elevate only the privileged hosts-file
            // operations on demand via the bundled helper. Required for the Microsoft Store.
            Elevation.PrivilegedFileOperations.UseElevationHelper();

            _mainForm = new MainForm();
            Application.Run(_mainForm);
        }
        else
        {
            ProgramSingleInstance.ShowFirstInstance();
        }
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
