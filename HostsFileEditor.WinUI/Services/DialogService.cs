using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HostsFileEditor.Services;

public class DialogService
{
    public async Task ShowErrorAsync(XamlRoot xamlRoot, string title, string message)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };

        await dlg.ShowAsync();
    }

    public async Task<bool> ShowConfirmationAsync(XamlRoot xamlRoot, string title, string message, string primaryText = "Yes", string closeText = "No")
    {
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = primaryText,
            CloseButtonText = closeText
        };

        var result = await dlg.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    public async Task<string?> ShowInputAsync(XamlRoot xamlRoot, string title, string placeholder, string okText = "OK", string cancelText = "Cancel")
    {
        var input = new TextBox { PlaceholderText = placeholder };
        var dlg = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = input,
            PrimaryButtonText = okText,
            CloseButtonText = cancelText
        };

        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return input.Text;
        }

        return null;
    }
}
