using HostsFileEditor.Properties;

namespace HostsFileEditor;

public class HostsArchive
{
    public HostsArchive()
    {
        FilePath = string.Empty;
    }

    public HostsArchive(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        // Use effective archive directory (allows test override)
        FilePath = Path.Combine(HostsArchiveList.EffectiveArchiveDirectory, name);
    }

    public string FilePath
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = string.Empty;

    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Orders archives by file name, case-insensitively — the single ordering both editions' archive
    /// lists use (classic sorts its <c>BindingListView</c> by it, modern sorts <c>RefreshArchives</c>
    /// by it). Shared here so the two can't silently diverge (the modern list once sorted by
    /// file-system enumeration order while classic sorted by name).
    /// </summary>
    public static IComparer<HostsArchive> FileNameComparer { get; } =
        Comparer<HostsArchive>.Create(
            static (a, b) => string.Compare(a?.FileName, b?.FileName, StringComparison.OrdinalIgnoreCase));

    public static bool Validate(string filePath, out string error)
    {
        error = string.Empty;

        try
        {
            _ = new FileInfo(filePath);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        // Compare by file name (case-insensitive, like the Windows file system) against
        // the effective archive directory so the test override is honored and a bare name
        // or a full path both validate correctly.
        var fileName = Path.GetFileName(filePath);
        var archiveDirectory = HostsArchiveList.EffectiveArchiveDirectory;

        if (Directory.Exists(archiveDirectory)
            && Directory.EnumerateFiles(archiveDirectory)
                .Any(existing => string.Equals(Path.GetFileName(existing), fileName, StringComparison.OrdinalIgnoreCase)))
        {
            error = Resources.ArchiveExists;
            return false;
        }

        return true;
    }
}
