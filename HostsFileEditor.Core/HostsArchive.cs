using HostsFileEditor.Properties;

namespace HostsFileEditor;

public class HostsArchive
{
    private string filePath = string.Empty;

    public HostsArchive()
    {
        FilePath = string.Empty;
    }

    public HostsArchive(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        FilePath = Path.Combine(HostsArchiveList.ArchiveDirectory, name);
    }

    public string FilePath
    {
        get => filePath;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            filePath = value;
        }
    }

    public string FileName => FilePath
        .Split(Path.DirectorySeparatorChar)
        .LastOrDefault() ?? string.Empty;

    public static bool Validate(string filePath, out string error)
    {
        bool isValid = false;

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
