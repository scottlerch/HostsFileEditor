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

    private readonly string _filePath;

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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static HostsFile Instance => _instance.Value;

    public static bool IsEnabled => File.Exists(DefaultHostFilePath);

    public static bool RemoveDefaultText { get; set; }

    public int EnabledCount => Entries.Count(entry => entry.Enabled);

    public HostsEntryList Entries { get; private set; }

    public int LineCount => Entries.Count;

    public static void DisableHostsFile()
    {
        PrivilegedFileOperations.Current.Move(DefaultHostFilePath, DefaultDisabledHostFilePath);
        NativeMethods.FlushDns();
    }

    public static void EnableHostsFile()
    {
        PrivilegedFileOperations.Current.Move(DefaultDisabledHostFilePath, DefaultHostFilePath);
        NativeMethods.FlushDns();
    }

    public void Import(string importFilePath)
    {
        if (_filePath != importFilePath)
        {
            Entries.BatchUpdate(() =>
            {
                Entries.Clear();
                Entries.AddLines(File.ReadAllLines(importFilePath), RemoveDefaultText);
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
        UndoManager.Instance.ClearHistory();

        Entries.BatchUpdate(() =>
        {
            Entries.Clear();
            Entries.AddLines(File.ReadAllLines(_filePath), removeDefault);
        });

        NativeMethods.FlushDns();
    }

    protected void OnPropertyChanged(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    private void OnHostsEntriesListChanged(object? sender, ListChangedEventArgs e)
    {
        OnPropertyChanged(nameof(LineCount));
        OnPropertyChanged(nameof(EnabledCount));
    }
}
