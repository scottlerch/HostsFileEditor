// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

namespace HostsFileEditor.Elevation;

/// <summary>
/// Thrown when a privileged operation could not complete because the user declined the
/// elevation (UAC) prompt. Callers can catch this to report a friendly "permission
/// required" message rather than a hard error.
/// </summary>
public sealed class ElevationCancelledException : Exception
{
    public ElevationCancelledException()
        : base("The operation was cancelled because administrator permission was not granted.")
    {
    }

    public ElevationCancelledException(string message)
        : base(message)
    {
    }

    public ElevationCancelledException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
