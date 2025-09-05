using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace HostsFileEditor;

public sealed partial class MainWindow
{
    // Reusable helper: check whether a TextBox currently has focus
    private bool IsTextBoxFocused() => FocusManager.GetFocusedElement() is TextBox;

    // Common accelerator guard: avoid handling accelerator when typing in a TextBox
    private void TryInvokeUnlessTextBox(Action action, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextBoxFocused())
        {
            return;
        }

        action();
        args.Handled = true;
    }

    // Simple error dialog helper to reduce duplicated try/catch UI code
    private async Task ShowErrorDialogAsync(string title, string message)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = title,
            Content = message,
            CloseButtonText = "OK"
        };

        await dlg.ShowAsync();
    }

    // Helper to interpret toggle sender types used across several handlers
    private static bool GetIsCheckedFromSender(object sender)
        => sender is AppBarToggleButton { IsChecked: true } || sender is ToggleMenuFlyoutItem { IsChecked: true };

    // Abstracts the repeated pattern of applying a toggle setting and persisting it
    private void ApplyToggleSetting(string localKey, Action<bool> applyAction, string propertyName, object sender)
    {
        var isChecked = GetIsCheckedFromSender(sender);
        applyAction(isChecked);
        LocalSettings.SetBool(localKey, isChecked);
        OnPropertyChanged(propertyName);
    }
}
