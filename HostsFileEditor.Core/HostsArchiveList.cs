using HostsFileEditor.Extensions;
using HostsFileEditor.Utilities;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace HostsFileEditor;

public class HostsArchiveList : BindingList<HostsArchive>
{
    public static readonly string ArchiveDirectory =
        Path.Combine(HostsFile.AppDataDirectory, "archive");

    // Previous location (under the protected hosts directory). Archives created by older
    // versions are migrated from here to the per-user directory on first use.
    private static readonly string LegacyArchiveDirectory =
        Path.Combine(HostsFile.DefaultHostFileDirectory, "archive");

    // Test hook to override archive directory for safe unit testing
    internal static string? TestArchiveDirectoryOverride { get; set; }

    internal static string EffectiveArchiveDirectory => TestArchiveDirectoryOverride ?? ArchiveDirectory;

    private static readonly Lazy<HostsArchiveList> _instance =
        new(() => new HostsArchiveList());

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList used only for simple collection change notifications; PropertyDescriptor reflection not exercised.")]
    private HostsArchiveList()
    {
        MigrateLegacyArchives();
        Refresh();
    }

    public static HostsArchiveList Instance => _instance.Value;

    /// <summary>
    /// Finds an archive by its file name, case-insensitively (the Windows file system's own rule and
    /// the same comparison <see cref="HostsArchive.FileNameComparer"/> uses). Returns
    /// <see langword="null"/> when no archive matches. Used by the command-line preset switch (issue
    /// #2) to resolve a name like <c>MyHosts1</c> to the archive to apply; matches the exact file name
    /// the archive was saved under (archives are stored as a file named for the archive).
    /// </summary>
    public HostsArchive? FindByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return this.FirstOrDefault(
            archive => string.Equals(archive.FileName, name, StringComparison.OrdinalIgnoreCase));
    }

    public void Delete(HostsArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

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

    // Best-effort, no-admin migration of archives from the legacy System32 location to the
    // per-user directory. Reading the legacy directory and writing the per-user directory
    // both work without elevation. Skipped when a test override is active.
    private static void MigrateLegacyArchives()
    {
        if (TestArchiveDirectoryOverride is not null)
        {
            return;
        }

        try
        {
            if (!Directory.Exists(LegacyArchiveDirectory) || Directory.Exists(ArchiveDirectory))
            {
                return;
            }

            Directory.CreateDirectory(ArchiveDirectory);

            foreach (var file in Directory.GetFiles(LegacyArchiveDirectory))
            {
                var destination = Path.Combine(ArchiveDirectory, Path.GetFileName(file));
                if (!File.Exists(destination))
                {
                    File.Copy(file, destination);
                }
            }
        }
        catch (Exception)
        {
            // Migration is best-effort and must never block startup.
        }
    }
}
