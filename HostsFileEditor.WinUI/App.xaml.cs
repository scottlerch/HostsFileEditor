using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace HostsFileEditor;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        this.InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Single instance using AppInstance
        var keyInstance = AppInstance.FindOrRegisterForKey("HostsFileEditor.SingleInstance");
        if (!keyInstance.IsCurrent)
        {
            _ = keyInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            // Exit this instance
            Environment.Exit(0);
            return;
        }

        _window = new MainWindow();
        _window.Activate();
    }
}
