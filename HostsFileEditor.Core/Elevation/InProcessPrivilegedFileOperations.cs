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
    public void WriteAllLines(string path, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(lines);

        using (FileEx.DisableAttributes(path, FileAttributes.ReadOnly))
        {
            File.WriteAllLines(path, lines);
        }
    }

    public void Move(string sourcePath, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(destinationPath);

        using (FileEx.DisableAttributes(sourcePath, FileAttributes.ReadOnly))
        {
            File.Move(sourcePath, destinationPath);
        }
    }
}
