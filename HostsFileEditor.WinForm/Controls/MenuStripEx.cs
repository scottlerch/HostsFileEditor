using System.ComponentModel;

namespace HostsFileEditor.Controls;

/// <summary>
/// This class adds on to the functionality provided in 
/// System.Windows.Forms.MenuStrip.
/// </summary>
/// <remarks>
/// Click through feature from:
/// http://blogs.msdn.com/b/rickbrew/archive/2006/01/09/511003.aspx
/// </remarks>
internal class MenuStripEx : MenuStrip
{
    /// <summary>
    /// Gets or sets whether the MenuStripEx honors item clicks when 
    /// its containing form does not have input focus.
    /// </summary>
    /// <remarks>
    /// Default value is false, which is the same behavior provided by
    /// the base MenuStrip class.
    /// </remarks>
    [DefaultValue(false)]
    public bool ClickThrough { get; set; }

    /// <inheritdoc />
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (ClickThrough &&
            m.Msg == NativeConstants.WmMouseActivate &&
            m.Result == (IntPtr)NativeConstants.MaActivateAndEat)
        {
            m.Result = (IntPtr)NativeConstants.MaActivate;
        }
    }

    /// <summary>
    /// Native constants needed by WndProc.
    /// </summary>
    private static class NativeConstants
    {
        internal const uint WmMouseActivate = 0x21;
        internal const uint MaActivate = 1;
        internal const uint MaActivateAndEat = 2;
        internal const uint MaNoActivate = 3;
        internal const uint MaNoActivateAndEat = 4;
    }
}
