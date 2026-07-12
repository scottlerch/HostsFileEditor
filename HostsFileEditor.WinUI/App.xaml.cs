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
            // Hand this activation to the already-running instance, then exit. The redirect MUST run
            // off the UI thread: RedirectActivationToAsync is a cross-process COM call, and blocking
            // this STA thread with .GetResult() deadlocks it (the thread can't pump COM to complete
            // the redirect) — so the running instance never receives the activation and appears to
            // hang when e.g. a Jump List preset is clicked while it's open. Run it on a thread-pool
            // (MTA) thread and wait via a semaphore, the canonical single-instance pattern.
            RedirectActivationTo(AppInstance.GetCurrent().GetActivatedEventArgs(), keyInstance);
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

    // Performs the cross-process activation redirect on a thread-pool thread and blocks this
    // (soon-to-exit) instance until it completes — never on the UI/STA thread (see OnLaunched).
    private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        using var redirected = new SemaphoreSlim(0, 1);
        _ = Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().GetAwaiter().GetResult();
            redirected.Release();
        });
        redirected.Wait();
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
