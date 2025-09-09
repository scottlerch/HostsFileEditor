using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using Windows.UI; // For Color

namespace HostsFileEditor;

public sealed class ValidityToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool valid)
        {
            if (valid)
            {
                // Standard background for normal items
                return Application.Current.Resources["LayerFillColorDefaultBrush"] as Brush
                       ?? new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF));
            }

            // Try to use the standard WinUI critical/error background token
            if (Application.Current.Resources.TryGetValue("SystemFillColorCriticalBackgroundBrush", out var critical) && critical is Brush criticalBrush)
            {
                return criticalBrush;
            }

            // Fallback: subtle error background (light red) if token not found
            return new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xE5, 0xE5));
        }

        return new SolidColorBrush(Color.FromArgb(0x00, 0, 0, 0));
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotSupportedException();
}
