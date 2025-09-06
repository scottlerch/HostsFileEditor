using HostsFileEditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace HostsFileEditor;

public partial class App : Application
{
    private Window? _window;
    internal IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        InitializeComponent();

        var services = new ServiceCollection();
        services.AddSingleton<DialogService>();
        services.AddSingleton<AnimationService>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Single instance using AppInstance
        var keyInstance = AppInstance.FindOrRegisterForKey("HostsFileEditor.SingleInstance");
        if (!keyInstance.IsCurrent)
        {
            _ = keyInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs());
            Environment.Exit(0);
            return;
        }

        var mw = Services.GetRequiredService<MainWindow>();

        _window = mw;
        _window.Activate();
    }
}
