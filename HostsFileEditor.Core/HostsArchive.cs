using HostsFileEditor.Properties;

namespace HostsFileEditor;

public class HostsArchive
{
    private string _filePath = string.Empty;

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
        get => _filePath;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _filePath = value;
        }
    }

    public string FileName => FilePath
        .Split(Path.DirectorySeparatorChar)
        .LastOrDefault() ?? string.Empty;

    public static bool Validate(string filePath, out string error)
    {
        var isValid = false;

        error = string.Empty;

        try
        {
            _ = new FileInfo(filePath);
            isValid = true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (isValid)
        {
            if (Directory.Exists(HostsArchiveList.ArchiveDirectory))
            {
                if (Directory.GetFiles(HostsArchiveList.ArchiveDirectory)
                    .Select(fullFilePath => Path.GetFileName(fullFilePath))
                    .Contains(filePath))
                {
                    isValid = false;
                    error = Resources.ArchiveExists;
                }
            }
        }

        return isValid;
    }
}
