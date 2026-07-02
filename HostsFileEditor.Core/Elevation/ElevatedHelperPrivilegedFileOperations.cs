// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace HostsFileEditor.Elevation;

/// <summary>
/// Performs privileged file operations by first attempting them in-process and, on
/// access-denied, delegating to a small helper executable launched with the "runas"
/// verb (which triggers a UAC prompt). This lets the host application run as a standard
/// user (asInvoker) — required for the Microsoft Store — while still being able to write
/// the system hosts file on demand. Targets the user already has access to (exports,
/// per-user archives) are written directly, so they never prompt for elevation.
/// </summary>
public sealed class ElevatedHelperPrivilegedFileOperations : IPrivilegedFileOperations
{
    /// <summary>ERROR_CANCELLED — returned by ShellExecute when the user declines the UAC prompt.</summary>
    private const int ErrorCancelled = 1223;

    /// <summary>
    /// Upper bound on the elevated operation itself. The UAC prompt is answered inside
    /// Process.Start (before WaitForExit), so this only bounds the copy/move + DNS flush,
    /// which take well under a second; a longer wait means the helper has wedged (e.g. a
    /// locked file) and we surface an error rather than hang the UI thread forever.
    /// </summary>
    private const int HelperTimeoutMilliseconds = 30_000;

    private readonly InProcessPrivilegedFileOperations _direct = new();
    private readonly string _helperPath;

    public ElevatedHelperPrivilegedFileOperations(string helperPath)
    {
        ArgumentNullException.ThrowIfNull(helperPath);
        _helperPath = helperPath;
    }

    public void WriteAllLines(string path, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(lines);

        // Materialize once so the payload is identical across the direct and elevated attempts.
        var materialized = lines as IReadOnlyList<string> ?? lines.ToList();

        try
        {
            _direct.WriteAllLines(path, materialized);
        }
        catch (UnauthorizedAccessException)
        {
            // Stage the new content in a user-writable temp file, then let the elevated
            // helper copy it into place. ShellExecute("runas") cannot redirect stdin.
            var payloadPath = Path.Combine(
                Path.GetTempPath(),
                "HostsFileEditor_" + Guid.NewGuid().ToString("N") + ".tmp");

            try
            {
                // Write inside the try so a failure mid-write still removes the partial file.
                File.WriteAllLines(payloadPath, materialized);
                RunHelper("write", path, payloadPath);
            }
            finally
            {
                TryDelete(payloadPath);
            }
        }
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(destinationPath);

        try
        {
            _direct.Move(sourcePath, destinationPath);
        }
        catch (UnauthorizedAccessException)
        {
            RunHelper("move", sourcePath, destinationPath);
        }
    }

    private void RunHelper(string command, string first, string second)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _helperPath,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
            Arguments = string.Format(
                CultureInfo.InvariantCulture,
                "{0} \"{1}\" \"{2}\"",
                command,
                first,
                second),
        };

        Process process;
        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start the elevation helper process.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            throw new ElevationCancelledException();
        }

        using (process)
        {
            if (!process.WaitForExit(HelperTimeoutMilliseconds))
            {
                // The elevated op wedged (e.g. a locked file). Best-effort kill (may be denied
                // since the child is higher-integrity) and surface an error so the UI recovers.
                try
                {
                    process.Kill();
                }
                catch (Exception)
                {
                    // Ignore — we still report the timeout below.
                }

                throw new IOException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The elevated file operation '{0}' timed out.",
                    command));
            }

            if (process.ExitCode != 0)
            {
                throw new IOException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The elevated file operation '{0}' failed with exit code {1}.",
                    command,
                    process.ExitCode));
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
