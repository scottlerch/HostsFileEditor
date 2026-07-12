using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace HostsFileEditor;

internal static class HostEntryBrushHelper
{
    public static Brush Get(HostsEntry entry)
    {
        if (entry is null)
        {
            return new SolidColorBrush(Colors.Transparent);
        }

        return entry.HasCommentOnly
            ? GetFirstExistingBrush(
                "SystemFillColorNeutralBackgroundBrush",
                "LayerFillColorTertiaryBrush",
                "LayerFillColorDefaultBrush")
                ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0))
            : entry.Valid
            ? GetResourceBrush("LayerFillColorDefaultBrush")
                   ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))
            : Application.Current.Resources.TryGetValue("SystemFillColorCriticalBackgroundBrush", out var critical)
            && critical is Brush criticalBrush
            ? criticalBrush
            : new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xE5, 0xE5));
    }

    /// <summary>
    /// Visibility of the per-row "ping failed" indicator (issue #9). Bound via x:Bind function binding
    /// to <see cref="HostsEntry.PingFailed"/>, so it re-evaluates when that flag changes.
    /// </summary>
    public static Visibility PingFailedVisibility(bool pingFailed) =>
        pingFailed ? Visibility.Visible : Visibility.Collapsed;

    private static Brush? GetResourceBrush(string key) => Application.Current.Resources[key] as Brush;

    private static Brush? GetFirstExistingBrush(params string[] keys)
    {
        foreach (var k in keys)
        {
            if (Application.Current.Resources.TryGetValue(k, out var obj) && obj is Brush b)
            {
                return b;
            }
        }
        return null;
    }
}
