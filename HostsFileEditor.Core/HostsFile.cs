using HostsFileEditor.Elevation;
using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using HostsFileEditor.Win32;
using System.ComponentModel;

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

    public static readonly string DefaultHostFilePath =
        Path.Combine(DefaultHostFileDirectory, @"hosts");

    public static readonly string DefaultBackupHostFilePath =
        Path.Combine(AppDataDirectory, "hosts.bak");

    public static readonly string DefaultDisabledHostFilePath =
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
    public bool IsModified => !ReferenceEquals(UndoManager.Instance.CurrentStateToken, _cleanStateToken);

    public event PropertyChangedEventHandler? PropertyChanged;

    public static HostsFile Instance => _instance.Value;

    public static bool IsEnabled => File.Exists(DefaultHostFilePath);

    public static bool RemoveDefaultText { get; set; }

    public int EnabledCount => Entries.Count(entry => entry.Enabled);

    public HostsEntryList Entries { get; private set; }

    public int LineCount => Entries.Count;

    public void DisableHostsFile()
    {
        PrivilegedFileOperations.Current.Move(DefaultHostFilePath, DefaultDisabledHostFilePath);
        NativeMethods.FlushDns();

        // The live file was renamed; track it so a subsequent Save/Refresh operates on the
        // now-disabled file instead of recreating the enabled one. Runs only after a
        // successful Move (a declined UAC prompt throws before this and leaves state intact).
        _filePath = DefaultDisabledHostFilePath;
    }

    public void EnableHostsFile()
    {
        PrivilegedFileOperations.Current.Move(DefaultDisabledHostFilePath, DefaultHostFilePath);
        NativeMethods.FlushDns();

        _filePath = DefaultHostFilePath;
    }

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

    public void Archive(string name)
    {
        var archive = new HostsArchive(name);
        SaveAs(archive.FilePath);
        HostsArchiveList.Instance.Add(archive);
    }

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
