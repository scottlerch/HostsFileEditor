using HostsFileEditor.CommandLine;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace HostsFileEditor;

/// <summary>
/// The modern edition's entry point (replaces the auto-generated XAML Main via
/// <c>DISABLE_XAML_GENERATED_MAIN</c>) so a headless command-line invocation (issue #2) can run and
/// exit before the WinUI stack is booted. With no command — or a Jump List preset launch — it starts
/// the app exactly as the generated Main did.
/// </summary>
public static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // Any argument that isn't a Jump List preset launch (open-archive:<path>) is a command: run it
        // against the launching console and exit without initializing WinUI (the ops are pure Core). An
        // unknown command reports an error rather than opening a window.
        if (args.Length > 0 && !args[0].StartsWith(TaskbarJumpList.OpenArchivePrefix, StringComparison.Ordinal))
        {
            ConsoleAttach.AttachToParentConsole();
            return HostsCli.Run(args, Console.Out, Console.Error);
        }

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(callbackParams =>
        {
            _ = callbackParams;
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }
}
