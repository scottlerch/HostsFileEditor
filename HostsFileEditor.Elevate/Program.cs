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

        try
        {
            switch (command)
            {
                case "write":
                    // first = destination (e.g. the hosts file), second = staged new content.
                    ClearReadOnly(first);
                    File.Copy(second, first, overwrite: true);
                    break;

                case "move":
                    // first = source, second = destination (enable/disable rename).
                    ClearReadOnly(first);
                    File.Move(first, second);
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

    private static void ClearReadOnly(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReadOnly))
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
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
