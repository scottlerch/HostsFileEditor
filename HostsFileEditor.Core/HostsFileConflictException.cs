namespace HostsFileEditor;

/// <summary>
/// Thrown when a hosts-file operation would destroy an existing saved configuration rather than
/// silently overwriting it. Currently raised by <see cref="HostsFile.DisableHostsFile"/> when a
/// <c>hosts.disabled</c> copy already exists whose contents differ from the live hosts file (e.g.
/// the live file was recreated by other software after a previous disable). Callers surface the
/// message and abort: the CLI prints it and exits non-zero, the GUIs show a dialog.
/// </summary>
public sealed class HostsFileConflictException : Exception
{
    public HostsFileConflictException()
    {
    }

    public HostsFileConflictException(string message)
        : base(message)
    {
    }

    public HostsFileConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
