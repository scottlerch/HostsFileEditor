namespace HostsFileEditor.Extensions;

/// <summary>
/// Helper Form extension methods.
/// </summary>
internal static class FormExtensions
{
    /// <summary>
    /// Show form if not visible, otherwise just Activate.
    /// </summary>
    /// <param name="form">
    /// The form to show or activate.
    /// </param>
    public static void ShowOrActivate(this Form form)
    {
        if (!form.Visible)
        {
            form.Show();
        }

        // Activate() alone doesn't un-minimize; restore first so a tray double-click on a
        // minimized window actually brings it back (matching the single-instance WndProc path).
        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.Activate();
    }
}
