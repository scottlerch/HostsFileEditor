using System.Runtime.InteropServices;

namespace HostsFileEditor.CommandLine;

/// <summary>
/// Lets a GUI-subsystem (WinExe) process write to the console that launched it (issue #2). Both
/// editions are windowed apps with no console of their own, so <c>Console.Out</c> goes nowhere until
/// we <c>AttachConsole(ATTACH_PARENT_PROCESS)</c> and re-point the standard streams at it — unless the
/// caller already redirected stdout (a pipe or file), in which case we leave it alone so
/// <c>HostsFileEditor.exe list &gt; out.txt</c> and <c>for /f ... ('HostsFileEditor.exe list')</c> work.
/// </summary>
public static partial class ConsoleAttach
{
    private const int AttachParentProcess = -1;
    private const int StdOutputHandle = -11;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    /// <summary>
    /// Ensures <c>Console.Out</c>/<c>Error</c> reach the caller. If stdout is already redirected, does
    /// nothing (the runtime already targets the redirected handle). Otherwise attaches to the launching
    /// console and rebinds the standard streams. A no-op when there is no console at all (e.g. launched
    /// from Explorer) — the command still runs and sets an exit code; its text just has nowhere to go.
    /// </summary>
    public static void AttachToParentConsole()
    {
        // Already have a real stdout (redirected to a pipe/file)? Leave it — Console.Out targets it.
        var stdout = GetStdHandle(StdOutputHandle);
        if (stdout != IntPtr.Zero && stdout != InvalidHandleValue)
        {
            return;
        }

        if (!AttachConsole(AttachParentProcess))
        {
            return;
        }

        // Rebind the standard streams to the freshly attached console (the runtime may have cached the
        // "no console" state). AutoFlush so output isn't lost when the process exits right after.
        // CA2000: these writers intentionally outlive this method — Console.SetOut/SetError take
        // ownership and they live for the rest of the (short-lived CLI) process.
#pragma warning disable CA2000
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError()) { AutoFlush = true });
#pragma warning restore CA2000
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AttachConsole(int dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial IntPtr GetStdHandle(int nStdHandle);
}
