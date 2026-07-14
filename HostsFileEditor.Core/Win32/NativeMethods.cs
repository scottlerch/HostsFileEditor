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

    private const int AppModelErrorNoPackage = 15700;

    [LibraryImport("kernel32.dll", EntryPoint = "GetCurrentPackageFullName")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetCurrentPackageFullName(ref int packageFullNameLength, IntPtr packageFullName);

    /// <summary>
    /// True when the process is running from an installed MSIX/AppX package (has package identity),
    /// false for the loose/portable build. Used to choose a version-independent launch path for the
    /// taskbar Jump List: a packaged app's <see cref="Environment.ProcessPath"/> is version-stamped
    /// (<c>…\WindowsApps\…_1.5.0.0_…\</c>) and goes stale on the next Store update.
    /// </summary>
    public static bool IsRunningPackaged()
    {
        var length = 0;
        // Query with a null buffer: unpackaged returns APPMODEL_ERROR_NO_PACKAGE; packaged returns
        // ERROR_INSUFFICIENT_BUFFER (or success), i.e. anything other than the no-package code.
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) != AppModelErrorNoPackage;
    }
}
