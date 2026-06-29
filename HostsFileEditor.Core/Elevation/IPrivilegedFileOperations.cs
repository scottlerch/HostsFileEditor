// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

namespace HostsFileEditor.Elevation;

/// <summary>
/// Abstraction over file operations that may require administrative rights, such as
/// writing the system hosts file or enabling/disabling it. Implementations decide
/// whether to perform the operation in-process or by delegating to an elevated helper.
/// </summary>
public interface IPrivilegedFileOperations
{
    /// <summary>Writes <paramref name="lines"/> to <paramref name="path"/>.</summary>
    void WriteAllLines(string path, IEnumerable<string> lines);

    /// <summary>Moves <paramref name="sourcePath"/> to <paramref name="destinationPath"/>.</summary>
    void Move(string sourcePath, string destinationPath);
}
