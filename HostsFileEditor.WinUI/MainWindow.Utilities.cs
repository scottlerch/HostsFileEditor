using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace HostsFileEditor;

public sealed partial class MainWindow
{
    private bool IsTextBoxFocused() => FocusManager.GetFocusedElement() is TextBox;

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
                OnPropertiesChanged(nameof(IsArchiveVisible), nameof(ArchivesColumnWidth), nameof(IsBackEnabled), nameof(MainViewVisibility), nameof(ArchiveViewVisibility));

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
                OnPropertiesChanged(nameof(IsArchiveVisible), nameof(ArchivesColumnWidth), nameof(IsBackEnabled), nameof(MainViewVisibility), nameof(ArchiveViewVisibility));
            }
        }
        finally
        {
            _isAnimatingArchive = false;
        }
    }
}
