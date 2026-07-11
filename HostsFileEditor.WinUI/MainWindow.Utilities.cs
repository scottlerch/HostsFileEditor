using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace HostsFileEditor;

public sealed partial class MainWindow
{
    // The parameterless FocusManager.GetFocusedElement() relies on CoreWindow and always
    // returns null in a WinUI 3 desktop app; the XamlRoot overload is required, otherwise the
    // guard never fires and accelerators hijack text editing in the filter box and cells.
    private bool IsTextBoxFocused() => FocusManager.GetFocusedElement(Content.XamlRoot) is TextBox;

    private void TryInvokeUnlessTextBox(Action action, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextBoxFocused())
        {
            return;
        }

        action();
        args.Handled = true;
    }

    private async Task ShowErrorDialogAsync(string title, string message) =>
        await _dialogService.ShowErrorAsync(Content.XamlRoot, title, message);

    // Neutral title/message dialog (same single-button ContentDialog as the error path — the service
    // applies no error-specific styling); used for informational results such as a merge summary.
    private async Task ShowInfoDialogAsync(string title, string message) =>
        await _dialogService.ShowErrorAsync(Content.XamlRoot, title, message);

    private async Task<bool> ShowConfirmationAsync(string title, string message, string primaryText = "Yes", string closeText = "No") =>
        await _dialogService.ShowConfirmationAsync(Content.XamlRoot, title, message, primaryText, closeText);

    private static bool GetIsCheckedFromSender(object sender)
        => sender is AppBarToggleButton { IsChecked: true } || sender is ToggleMenuFlyoutItem { IsChecked: true };

    private void ApplyToggleSetting(string localKey, Action<bool> applyAction, string propertyName, object sender)
    {
        var isChecked = GetIsCheckedFromSender(sender);
        applyAction(isChecked);
        LocalSettings.SetBool(localKey, isChecked);
        OnPropertyChanged(propertyName);
    }

    private void OnPropertiesChanged(params string[] propertyNames)
    {
        foreach (var name in propertyNames.Where(n => !string.IsNullOrEmpty(n)))
        {
            OnPropertyChanged(name);
        }
    }

    private async Task ToggleArchiveVisibilityAsync(bool show)
    {
        if (_isAnimatingArchive)
        {
            return;
        }

        _isAnimatingArchive = true;
        try
        {
            if (show)
            {
                IsArchiveVisible = true;
                ArchivesColumnWidth = new GridLength(1, GridUnitType.Star);
                LocalSettings.SetBool("ArchiveVisible", true);
                OnPropertiesChanged(nameof(IsArchiveVisible), nameof(ArchivesColumnWidth), nameof(IsBackEnabled), nameof(MainViewVisibility), nameof(ArchiveViewVisibility), nameof(StatusRowHeight));

                if (ArchiveHost.Visibility == Visibility.Visible)
                {
                    // use injected animation service
                    await _animationService.SlideInAsync(ArchiveHost, fromRight: true);
                }
            }
            else
            {
                if (ArchiveHost.Visibility == Visibility.Visible)
                {
                    await _animationService.SlideOutAsync(ArchiveHost, toRight: true);
                }

                IsArchiveVisible = false;
                ArchivesColumnWidth = new GridLength(0);
                LocalSettings.SetBool("ArchiveVisible", false);
                OnPropertiesChanged(nameof(IsArchiveVisible), nameof(ArchivesColumnWidth), nameof(IsBackEnabled), nameof(MainViewVisibility), nameof(ArchiveViewVisibility), nameof(StatusRowHeight));
            }
        }
        finally
        {
            _isAnimatingArchive = false;
        }
    }
}
