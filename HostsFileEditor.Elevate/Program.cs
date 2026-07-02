// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

using System.Runtime.InteropServices;

namespace HostsFileEditor.Elevate;

/// <summary>
/// Tiny elevation helper for Hosts File Editor. The main application launches this with
/// the "runas" verb (which triggers a UAC prompt), so it runs as administrator, performs a
/// single privileged hosts-file operation, and exits. The application itself stays
/// asInvoker — required for Microsoft Store distribution.
/// </summary>
/// <remarks>
/// Usage:
///   HostsFileEditor.Elevate.exe write &lt;destinationPath&gt; &lt;payloadPath&gt;
///   HostsFileEditor.Elevate.exe move  &lt;sourcePath&gt;      &lt;destinationPath&gt;
/// Exit codes: 0 = success, 1 = invalid arguments, 2 = operation failed.
/// </remarks>
internal static partial class Program
{
    private const int Success = 0;
    private const int InvalidArguments = 1;
    private const int OperationFailed = 2;

    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            return InvalidArguments;
        }

        var command = args[0];
        var first = args[1];
        var second = args[2];

        // This helper runs elevated (launched via runas). Restrict it to exactly the operations
        // the app needs on the known hosts-file paths so it cannot be abused as a general-purpose
        // "write/move any file as administrator" tool. Anything else is rejected before any I/O.
        var etcDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"System32\drivers\etc");
        var hostsPath = Path.Combine(etcDirectory, "hosts");
        var disabledPath = hostsPath + ".disabled";

        try
        {
            switch (command)
            {
                case "write":
                    // first = destination (the hosts or hosts.disabled file);
                    // second = the staged new content (must be an absolute path).
                    if (!IsHostsPath(first, hostsPath, disabledPath) || !Path.IsPathFullyQualified(second))
                    {
                        return InvalidArguments;
                    }

                    ClearBlockingAttributes(first);
                    File.Copy(second, first, overwrite: true);
                    break;

                case "move":
                    // Only the enable/disable rename between hosts and hosts.disabled is allowed.
                    if (!IsEnableDisableRename(first, second, hostsPath, disabledPath))
                    {
                        return InvalidArguments;
                    }

                    ClearBlockingAttributes(first);
                    File.Move(first, second, overwrite: true);
                    break;

                default:
                    return InvalidArguments;
            }

            FlushDns();
            return Success;
        }
        catch (Exception)
        {
            return OperationFailed;
        }
    }

    private static bool IsHostsPath(string path, string hostsPath, string disabledPath) =>
        PathEquals(path, hostsPath) || PathEquals(path, disabledPath);

    private static bool IsEnableDisableRename(string source, string destination, string hostsPath, string disabledPath) =>
        (PathEquals(source, hostsPath) && PathEquals(destination, disabledPath)) ||
        (PathEquals(source, disabledPath) && PathEquals(destination, hostsPath));

    private static bool PathEquals(string path, string expected)
    {
        // Reject relative paths outright — a runas-launched process starts in System32, so a
        // relative path would resolve there. Compare fully-normalized, case-insensitively.
        if (string.IsNullOrEmpty(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        return string.Equals(
            Path.GetFullPath(path),
            Path.GetFullPath(expected),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearBlockingAttributes(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        const FileAttributes blocking = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System;
        var attributes = File.GetAttributes(path);
        if ((attributes & blocking) != 0)
        {
            File.SetAttributes(path, attributes & ~blocking);
        }
    }

    private static void FlushDns()
    {
        try
        {
            _ = DnsFlushResolverCache();
        }
        catch (Exception)
        {
            // Flushing the DNS cache is best-effort and must not fail the operation.
        }
    }

    [LibraryImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial uint DnsFlushResolverCache();
}
