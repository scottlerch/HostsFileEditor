using HostsFileEditor.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System.Runtime.InteropServices;

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

    // Performs the cross-process activation redirect on a thread-pool thread while THIS (STA) thread
    // waits with CoWaitForMultipleObjects — a COM-aware wait that keeps pumping the STA message loop.
    // A plain blocking wait (SemaphoreSlim/WaitHandle) would stall the STA pump, and the redirect is a
    // cross-apartment COM call that needs it — so it would deadlock (the running instance never gets
    // the activation and hangs). This is the pattern from Microsoft's WinUI single-instance sample.
    private static void RedirectActivationTo(AppActivationArguments args, AppInstance keyInstance)
    {
        var redirectDone = CreateEventW(IntPtr.Zero, bManualReset: true, bInitialState: false, lpName: null);
        _ = Task.Run(() =>
        {
            keyInstance.RedirectActivationToAsync(args).AsTask().GetAwaiter().GetResult();
            SetEvent(redirectDone);
        });

        _ = CoWaitForMultipleObjects(CwmoDefault, Infinite, 1, [redirectDone], out _);
        CloseHandle(redirectDone);
    }

    private const uint CwmoDefault = 0;
    private const uint Infinite = 0xFFFFFFFF;

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr CreateEventW(IntPtr lpEventAttributes, [MarshalAs(UnmanagedType.Bool)] bool bManualReset, [MarshalAs(UnmanagedType.Bool)] bool bInitialState, string? lpName);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetEvent(IntPtr hEvent);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    [LibraryImport("ole32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int CoWaitForMultipleObjects(uint dwFlags, uint dwMilliseconds, uint nHandles, IntPtr[] pHandles, out uint lpdwIndex);

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
