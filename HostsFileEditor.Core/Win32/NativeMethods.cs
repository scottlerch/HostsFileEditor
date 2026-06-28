using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace HostsFileEditor.Win32;

public static partial class NativeMethods
{
    public const int HwndBroadcast = 0xffff;

    [LibraryImport("user32", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial int RegisterWindowMessage(string message);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam);

    public static int RegisterWindowMessage(string format, params object[] args)
    {
        var message = string.Format(CultureInfo.InvariantCulture, format, args);
        return RegisterWindowMessage(message);
    }

    [LibraryImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint DnsFlushResolverCache();

    public static void FlushDns()
    {
        var result = DnsFlushResolverCache();
        Debug.WriteLine($"DnsFlushResolverCache: {result}");
    }
}
