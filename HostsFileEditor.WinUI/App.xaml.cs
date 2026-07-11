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

        // Run as a standard user (asInvoker) and elevate only the privileged hosts-file
        // operations on demand via the bundled helper. Required for the Microsoft Store.
        Elevation.PrivilegedFileOperations.UseElevationHelper();

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
            // Hand this activation to the already-running instance, then exit. Wait for the
            // redirect to complete instead of racing it with Environment.Exit (fire-and-forget).
            keyInstance.RedirectActivationToAsync(AppInstance.GetCurrent().GetActivatedEventArgs())
                .AsTask().GetAwaiter().GetResult();
            Environment.Exit(0);
            return;
        }

        // Bring our window to the foreground when another instance is launched and redirects to us.
        keyInstance.Activated += OnInstanceActivated;

        var mw = Services.GetRequiredService<MainWindow>();

        _window = mw;

        // If launched from a taskbar Jump List preset (issue #10), hand the archive path to the window
        // to import once it finishes loading.
        var openArchivePath = TaskbarJumpList.TryGetOpenArchivePath(AppInstance.GetCurrent().GetActivatedEventArgs());
        if (openArchivePath is not null)
        {
            mw.RequestOpenArchive(openArchivePath);
        }

        _window.Activate();
    }

    private void OnInstanceActivated(object? sender, AppActivationArguments e) =>
        // Raised on a background thread; marshal to the UI thread to activate the window, and — if
        // this activation came from a Jump List preset (issue #10) — open that preset in the already-
        // running instance (the redirect carries the launch arguments).
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _window?.Activate();

            var openArchivePath = TaskbarJumpList.TryGetOpenArchivePath(e);
            if (openArchivePath is not null && _window is MainWindow mw)
            {
                mw.RequestOpenArchive(openArchivePath);
            }
        });
}
