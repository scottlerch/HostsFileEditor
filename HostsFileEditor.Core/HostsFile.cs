using HostsFileEditor.Elevation;
using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using HostsFileEditor.Win32;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace HostsFileEditor;

public class HostsFile : INotifyPropertyChanged
{
    public static readonly string DefaultHostFileDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            @"System32\drivers\etc");

    // Per-user application data directory (writable without administrator rights). The
    // hosts-file backup and archives live here so loading and archiving never need
    // elevation — only writing the live hosts file or enabling/disabling it does.
    public static readonly string AppDataDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HostsFileEditor");

    // Dev/test override: when the HFE_HOSTS_PATH environment variable points at an existing file,
    // the app loads (and saves/enables/disables) THAT file instead of the real system hosts file.
    // This lets load performance be measured against a large test hosts file without touching — or
    // wedging the DNS Client on — the real machine hosts file. Gated entirely on the env var and
    // the file existing, so a normal run (no such variable) is completely unaffected.
    private static readonly string? _hostsPathOverride = ResolveHostsPathOverride();

    private static string? ResolveHostsPathOverride()
    {
        // 1. Environment variable — easy for the portable exe launched from a shell.
        var env = Environment.GetEnvironmentVariable("HFE_HOSTS_PATH");
        if (!string.IsNullOrEmpty(env) && File.Exists(env))
        {
            return Path.GetFullPath(env);
        }

        // 2. A marker file whose first line is the hosts path — for the packaged/MSIX app, which
        //    doesn't reliably inherit a freshly set environment variable. Absent = normal behavior.
        try
        {
            var marker = Path.Combine(AppDataDirectory, "dev-hosts-path.txt");
            if (File.Exists(marker))
            {
                var path = File.ReadLines(marker).FirstOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return Path.GetFullPath(path);
                }
            }
        }
        catch
        {
            // Test hook only — never let it interfere with normal startup.
        }

        return null;
    }

    public static string DefaultHostFilePath =>
        _hostsPathOverride ?? Path.Combine(DefaultHostFileDirectory, @"hosts");

    /// <summary>
    /// Gets the dev/test override path (from <c>HFE_HOSTS_PATH</c> or the <c>dev-hosts-path.txt</c>
    /// marker) when one is active, otherwise <see langword="null"/>. Surfaced so the UI can make it
    /// obvious the app is editing an alternate file rather than the real system hosts file — the
    /// override ships in all builds, so this keeps its effect visible rather than silent.
    /// </summary>
    public static string? OverridePath => _hostsPathOverride;

    public static readonly string DefaultBackupHostFilePath =
        Path.Combine(AppDataDirectory, "hosts.bak");

    public static string DefaultDisabledHostFilePath =>
        DefaultHostFilePath + ".disabled";

    // Internal test hook: override backup file path so unit tests do not need elevated permissions
    internal static string? TestBackupHostFilePathOverride { get; set; }

    private static readonly Lazy<HostsFile> _instance =
        new(() =>
        {
            UndoManager.Instance.ClearHistory();

            return IsEnabled ? new HostsFile(DefaultHostFilePath) : new HostsFile(DefaultDisabledHostFilePath);
        });

    // Not readonly: Enable/Disable rename the live hosts file at runtime and update this so
    // Save/Refresh keep targeting the current file rather than the path frozen at construction.
    private string _filePath;

    // Undo position captured at the last point the in-memory entries matched the saved file
    // (load / refresh / save). IsModified compares the current position against it.
    private object? _cleanStateToken;

    private HostsFile(string filePath)
    {
        _filePath = filePath;

        if (!File.Exists(filePath))
        {
            Entries = [];
        }
        else
        {
            var backupPath = TestBackupHostFilePathOverride ?? DefaultBackupHostFilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
            using (FileEx.DisableAttributes(backupPath, FileAttributes.ReadOnly))
            {
                File.Copy(filePath, backupPath, true);
            }

            Entries = new HostsEntryList(File.ReadAllLines(filePath), RemoveDefaultText);
        }

        Entries.ListChanged += OnHostsEntriesListChanged;

        // Freshly loaded from disk: no unsaved changes.
        MarkClean();
    }

    // Records the current undo position as the "saved" state. Called when the in-memory entries
    // match the file on disk (initial load, refresh, save). Import/RestoreDefault deliberately
    // do NOT call this: they load content that has not yet been written to the hosts file, so
    // the app should still consider it modified (a save is needed).
    private void MarkClean() => _cleanStateToken = UndoManager.Instance.CurrentStateToken;

    /// <summary>
    /// Gets a value indicating whether there are in-memory edits that have not been saved to the
    /// hosts file, so the UI can warn before discarding them (e.g. on exit).
    /// </summary>
    /// <remarks>
    /// The clean token is a specific undo-history position (reference-compared). If enough edits
    /// push it out of the capped history (<see cref="UndoManager"/> trims oldest), it can never be
    /// reference-equal again — which is correct: those undo steps are gone, so the file genuinely
    /// cannot be returned to the saved state via undo and IsModified should stay true. That also
    /// holds when the clean token is the load-time EMPTY-history position: the UndoManager rebases
    /// its sentinel token on every eviction, so undoing all the way down after 1000+ edits does not
    /// falsely compare clean (the evicted edits are still applied and unsaved).
    /// </remarks>
    public bool IsModified => !ReferenceEquals(UndoManager.Instance.CurrentStateToken, _cleanStateToken);

    public event PropertyChangedEventHandler? PropertyChanged;

    public static HostsFile Instance => _instance.Value;

    /// <summary>
    /// Forces the lazy initial load and parse of the hosts file onto a background thread. For a
    /// very large hosts file (hundreds of thousands of entries) this parse takes long enough to
    /// freeze the UI if done inline, so the apps await this (showing a loading indicator) before
    /// first touching <see cref="Instance"/> on the UI thread. Subsequent access is instant.
    /// </summary>
    public static Task PreloadAsync() => Task.Run(() => _ = _instance.Value.Entries.Count);

    public static bool IsEnabled => File.Exists(DefaultHostFilePath);

    public static bool RemoveDefaultText { get; set; }

    public int EnabledCount => Entries.Count(entry => entry.Enabled);

    public HostsEntryList Entries { get; private set; }

    public int LineCount => Entries.Count;

    public void DisableHostsFile()
    {
        // Guard against silent data loss: disabling renames the live hosts file over
        // hosts.disabled (overwrite). If a hosts.disabled already exists whose contents differ
        // — e.g. some other software recreated the live hosts file after a previous disable, so
        // both files now exist — that Move would destroy the user's saved disabled configuration
        // (and, with the load-time backup already overwritten, its only copy). Refuse and let the
        // user resolve it, rather than losing data in a prompt-free scriptable command.
        if (DisableWouldOverwriteDifferentFile(DefaultHostFilePath, DefaultDisabledHostFilePath))
        {
            throw new HostsFileConflictException(
                $"A different disabled hosts file already exists ('{DefaultDisabledHostFilePath}'). " +
                "Disabling now would overwrite it and lose that saved configuration. Enable the hosts " +
                "file first to restore the disabled copy, or remove that file, then try again.");
        }

        PrivilegedFileOperations.Current.Move(DefaultHostFilePath, DefaultDisabledHostFilePath);
        NativeMethods.FlushDns();

        // The live file was renamed; track it so a subsequent Save/Refresh operates on the
        // now-disabled file instead of recreating the enabled one. Runs only after a
        // successful Move (a declined UAC prompt throws before this and leaves state intact).
        _filePath = DefaultDisabledHostFilePath;
    }

    // Internal for tests. Would disabling (moving <paramref name="livePath"/> over
    // <paramref name="disabledPath"/>) overwrite a DIFFERENT existing file, losing its contents?
    // True only when both files exist and their contents differ — a missing disabled file or
    // byte-identical contents is safe to overwrite. If the files exist but can't be read to
    // compare, err on the side of caution and report a conflict rather than risk the overwrite.
    internal static bool DisableWouldOverwriteDifferentFile(string livePath, string disabledPath)
    {
        if (!File.Exists(disabledPath) || !File.Exists(livePath))
        {
            return false;
        }

        try
        {
            return !FilesHaveSameContent(livePath, disabledPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool FilesHaveSameContent(string pathA, string pathB)
    {
        if (new FileInfo(pathA).Length != new FileInfo(pathB).Length)
        {
            return false;
        }

        using var streamA = File.OpenRead(pathA);
        using var streamB = File.OpenRead(pathB);
        Span<byte> bufferA = stackalloc byte[4096];
        Span<byte> bufferB = stackalloc byte[4096];

        while (true)
        {
            var readA = streamA.ReadAtLeast(bufferA, bufferA.Length, throwOnEndOfStream: false);
            var readB = streamB.ReadAtLeast(bufferB, bufferB.Length, throwOnEndOfStream: false);
            if (readA != readB)
            {
                return false;
            }

            if (readA == 0)
            {
                return true;
            }

            if (!bufferA[..readA].SequenceEqual(bufferB[..readB]))
            {
                return false;
            }
        }
    }

    public void EnableHostsFile()
    {
        PrivilegedFileOperations.Current.Move(DefaultDisabledHostFilePath, DefaultHostFilePath);
        NativeMethods.FlushDns();

        _filePath = DefaultHostFilePath;
    }

    // IL2026/IL3050: the BatchUpdate Reset walks BindingList's reflective descriptor machinery in
    // the trim/AOT analyzers' eyes only — the apps never data-bind through PropertyDescriptors, so
    // Enum.GetValues/TypeDescriptor paths are unreachable (same justification as HostsEntryList).
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList descriptor machinery unreachable; see comment.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "BindingList descriptor machinery unreachable; see comment.")]
    public void Import(string importFilePath)
    {
        if (_filePath != importFilePath)
        {
            // Read before mutating: a failed read (missing/locked file) then leaves the
            // current entries and undo history intact instead of clearing them and throwing
            // mid-batch (which would strand the UI showing rows that no longer exist).
            var lines = File.ReadAllLines(importFilePath);

            // Undo actions from before the import reference the replaced entries and stale
            // indices; replaying them would crash or corrupt, so drop the history.
            UndoManager.Instance.ClearHistory();

            Entries.BatchUpdate(() =>
            {
                Entries.Clear();
                Entries.AddLines(lines, RemoveDefaultText);
            });
        }
    }

    /// <summary>
    /// Merges another hosts file into the current entries, eliminating duplicates (issue #26). Reads
    /// the file (before mutating, like <see cref="Import"/>, so a failed read leaves the current entries
    /// intact), then appends its valid entries that are not already present (by canonical IP + host
    /// names, case-insensitive) — see <see cref="HostsEntryList.MergeLines"/>, which performs the append
    /// as a single undoable step. Returns the number of entries added; a merge that adds nothing leaves
    /// the list, the modified flag, and undo history untouched.
    /// </summary>
    public int Merge(string mergeFilePath)
    {
        var lines = File.ReadAllLines(mergeFilePath);
        return Entries.MergeLines(lines);
    }

    public void Archive(string name)
    {
        var archive = new HostsArchive(name);
        SaveAs(archive.FilePath);
        HostsArchiveList.Instance.Add(archive);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList descriptor machinery unreachable; see Import.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "BindingList descriptor machinery unreachable; see Import.")]
    public void RestoreDefault()
    {
        UndoManager.Instance.ClearHistory();

        Entries.BatchUpdate(() =>
        {
            Entries.Clear();
            Entries.AddLines(
                Resources.hosts.Split([Environment.NewLine], StringSplitOptions.None),
                false);
        });
    }

    public void Save()
    {
        SaveAs(_filePath);
        NativeMethods.FlushDns();

        // Entries now match the file on disk.
        MarkClean();
    }

    public void SaveAs(string saveFilePath)
    {
        FileInfo info = new(saveFilePath);

        if (string.IsNullOrWhiteSpace(info.DirectoryName))
        {
            throw new ArgumentException("Invalid file path.", nameof(saveFilePath));
        }

        if (!Directory.Exists(info.DirectoryName))
        {
            Directory.CreateDirectory(info.DirectoryName);
        }

        PrivilegedFileOperations.Current.WriteAllLines(
            saveFilePath,
            Entries.Select(entry => entry.UnparsedText));
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "BindingList descriptor machinery unreachable; see Import.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "BindingList descriptor machinery unreachable; see Import.")]
    public void Refresh(bool removeDefault = true)
    {
        // Read before mutating so a failed read leaves the current entries intact rather
        // than clearing them and throwing mid-batch (see Import).
        var lines = File.ReadAllLines(_filePath);

        UndoManager.Instance.ClearHistory();

        Entries.BatchUpdate(() =>
        {
            Entries.Clear();
            Entries.AddLines(lines, removeDefault);
        });

        NativeMethods.FlushDns();

        // Entries were just reloaded from the live file, so they match disk.
        MarkClean();
    }

    protected void OnPropertyChanged(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    private void OnHostsEntriesListChanged(object? sender, ListChangedEventArgs e)
    {
        OnPropertyChanged(nameof(LineCount));
        OnPropertyChanged(nameof(EnabledCount));
    }
}
