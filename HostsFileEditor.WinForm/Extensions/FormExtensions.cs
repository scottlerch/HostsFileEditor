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
        if (form.Visible)
        {
            form.Activate();
        }
        else
        {
            form.Show();
        }
    }
}