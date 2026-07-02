// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

using HostsFileEditor.Utilities;

namespace HostsFileEditor.Elevation;

/// <summary>
/// Performs privileged file operations directly in the current process. This succeeds
/// only when the process already has write access to the target — for example an
/// elevated build, or a user-writable destination such as an export or a per-user
/// archive. When access is denied it surfaces <see cref="UnauthorizedAccessException"/>,
/// which callers such as <see cref="ElevatedHelperPrivilegedFileOperations"/> use as the
/// signal to escalate.
/// </summary>
public sealed class InProcessPrivilegedFileOperations : IPrivilegedFileOperations
{
    // Clear ReadOnly and (rarely) Hidden/System for the duration of the write/move — the hosts
    // file is frequently read-only, and any of these attributes would otherwise block the op.
    private const FileAttributes BlockingAttributes =
        FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System;

    public void WriteAllLines(string path, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(lines);

        using (FileEx.DisableAttributes(path, BlockingAttributes))
        {
            File.WriteAllLines(path, lines);
        }
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(destinationPath);

        using (FileEx.DisableAttributes(sourcePath, BlockingAttributes))
        {
            // overwrite: the enable/disable rename can find a stale destination from an earlier
            // interrupted toggle; replacing it is the correct recovery rather than throwing.
            File.Move(sourcePath, destinationPath, overwrite: true);
        }
    }
}
