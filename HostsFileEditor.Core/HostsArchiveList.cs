using HostsFileEditor.Extensions;
using HostsFileEditor.Utilities;
using System.ComponentModel;

namespace HostsFileEditor;

public class HostsArchiveList : BindingList<HostsArchive>
{
    public static readonly string ArchiveDirectory =
        Path.Combine(HostsFile.DefaultHostFileDirectory, "archive");

    private static readonly Lazy<HostsArchiveList> _instance =
        new(() => new HostsArchiveList());

    private HostsArchiveList()
    {
        Refresh();
    }

    public static HostsArchiveList Instance => _instance.Value;

    public void Delete(HostsArchive archive)
    {
        using (FileEx.DisableAttributes(archive.FilePath, FileAttributes.ReadOnly))
        {
            File.Delete(archive.FilePath);
        }

        Remove(archive);
    }

    public void Refresh()
    {
        this.BatchUpdate(() =>
        {
            Clear();

            if (Directory.Exists(ArchiveDirectory))
            {
                var files = Directory.GetFiles(ArchiveDirectory);

                foreach (var file in files)
                {
                    Add(new HostsArchive { FilePath = file });
                }
            }
        });
    }
}
