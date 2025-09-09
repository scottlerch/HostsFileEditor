using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Windows.UI; // For Color

namespace HostsFileEditor.Converters;

public sealed class HostEntryToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        // New preferred path: value is a HostsEntry (binding changed to ".")
        if (value is HostsEntry entry)
        {
            // Entry is invalid.
            if (entry.HasCommentOnly)
            {
                // Use a neutral/darker system fill instead of the critical brush for pure comment lines.
                return GetFirstExistingBrush(
                    "SystemFillColorNeutralBackgroundBrush", // WinUI token (if available)
                    "LayerFillColorTertiaryBrush",           // Slightly stronger than default layer
                    "LayerFillColorDefaultBrush"             // Fallback to default layer
                ) ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xF0, 0xF0));
            }

            if (entry.Valid)
            {
                return GetResourceBrush("LayerFillColorDefaultBrush")
                       ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            }

            // Non-comment invalid entries: keep critical styling if available.
            if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBackgroundBrush", out var critical)
                && critical is Brush criticalBrush)
            {
                return criticalBrush;
            }

            // Fallback subtle error tint.
            return new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xE5, 0xE5));
        }

        // Backward compatibility: value might still be a bool in older bindings.
        if (value is bool valid)
        {
            if (valid)
            {
                return GetResourceBrush("LayerFillColorDefaultBrush")
                       ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            }
            if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBackgroundBrush", out var critical)
                && critical is Brush criticalBrush)
            {
                return criticalBrush;
            }
            return new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xE5, 0xE5));
        }

        // Transparent (no-op) default.
        return new SolidColorBrush(Color.FromArgb(0x00, 0, 0, 0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();

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
