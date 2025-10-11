using HostsFileEditor.Extensions;
using HostsFileEditor.Utilities;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace HostsFileEditor;

public class HostsArchiveList : BindingList<HostsArchive>
{
    public static readonly string ArchiveDirectory =
        Path.Combine(HostsFile.DefaultHostFileDirectory, "archive");

    // Test hook to override archive directory for safe unit testing
    internal static string? TestArchiveDirectoryOverride { get; set; }

    internal static string EffectiveArchiveDirectory => TestArchiveDirectoryOverride ?? ArchiveDirectory;

    private static readonly Lazy<HostsArchiveList> _instance =
        new(() => new HostsArchiveList());

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList used only for simple collection change notifications; PropertyDescriptor reflection not exercised.")]
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

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList used only for simple collection change notifications; PropertyDescriptor reflection not exercised.")]
    public void Refresh()
    {
        this.BatchUpdate(() =>
        {
            Clear();

            if (Directory.Exists(EffectiveArchiveDirectory))
            {
                var files = Directory.GetFiles(EffectiveArchiveDirectory);

                foreach (var file in files)
                {
                    Add(new HostsArchive { FilePath = file });
                }
            }
        });
    }
}
