// <copyright file="HostsFile.cs" company="N/A">
// Copyright 2025 Scott M. Lerch
// 
// This file is part of HostsFileEditor.
// 
// HostsFileEditor is free software: you can redistribute it and/or modify it 
// under the terms of the GNU General Public License as published by the Free 
// Software Foundation, either version 2 of the License, or (at your option)
// any later version.
// 
// HostsFileEditor is distributed in the hope that it will be useful, but 
// WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
// or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for
// more details.
// 
// You should have received a copy of the GNU General Public   License along
// with HostsFileEditor. If not, see http://www.gnu.org/licenses/.
// </copyright>

using HostsFileEditor.Extensions;
using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using HostsFileEditor.Win32;
using System.ComponentModel;
using System.Linq.Expressions;

namespace HostsFileEditor;

/// <summary>
/// This class represents a hosts file.
/// </summary>
internal class HostsFile : INotifyPropertyChanged
{
    /// <summary>
    /// The default hosts file directory location.
    /// </summary>
    public static readonly string DefaultHostFileDirectory = 
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows), 
            @"System32\drivers\etc");

    /// <summary>
    /// The default host file path.
    /// </summary>
    public static readonly string DefaultHostFilePath =
        Path.Combine(DefaultHostFileDirectory, @"hosts");

    /// <summary>
    /// The default disabled host file path.
    /// </summary>
    public static readonly string DefaultBackupHostFilePath = 
        DefaultHostFilePath + ".bak";

    /// <summary>
    /// The backup host file path.
    /// </summary>
    public static readonly string DefaultDisabledHostFilePath = 
        DefaultHostFilePath + ".disabled";

    /// <summary>
    /// The singleton instance of the hosts file.
    /// </summary>
    private static readonly Lazy<HostsFile> instance =
        new(() =>
        {
            UndoManager.Instance.ClearHistory();

            if (IsEnabled)
            {
                return new HostsFile(DefaultHostFilePath);
            }
            else
            {
                return new HostsFile(DefaultDisabledHostFilePath);
            }
        });

    /// <summary>
    /// The file path.
    /// </summary>
    private readonly string filePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="HostsFile"/> class.
    /// </summary>
    /// <param name="filePath">
    /// The file path.
    /// </param>
    private HostsFile(string filePath)
    {
        this.filePath = filePath;

        if (!File.Exists(filePath))
        {
            Entries = [];
        }
        else
        {
            using (FileEx.DisableAttributes(DefaultBackupHostFilePath, FileAttributes.ReadOnly))
            {
                File.Copy(filePath, DefaultBackupHostFilePath, true);
            }

            Entries = new HostsEntryList(File.ReadAllLines(filePath), RemoveDefaultText);
        }

        Entries.ListChanged += OnHostsEntriesListChanged;
    }

    /// <summary>
    /// The property changed.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static HostsFile Instance
    {
        get { return instance.Value; }
    }

    /// <summary>
    /// Gets a value indicating whether IsEnabled.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            return File.Exists(DefaultHostFilePath);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether to 
    /// remove default hosts file text.
    /// </summary>
    public static bool RemoveDefaultText { get; set; }

    /// <summary>
    /// Gets EnabledCount.
    /// </summary>
    public int EnabledCount
    {
        get
        {
            return Entries.Count(entry => entry.Enabled);
        }
    }

    /// <summary>
    /// Gets Entries.
    /// </summary>
    public HostsEntryList Entries { get; private set; }

    /// <summary>
    /// Gets LineCount.
    /// </summary>
    public int LineCount
    {
        get
        {
            return Entries.Count;
        }
    }

    /// <summary>
    /// The disable hosts file.
    /// </summary>
    public static void DisableHostsFile()
    {
        using (FileEx.DisableAttributes(DefaultHostFilePath, FileAttributes.ReadOnly))
        {
            File.Move(DefaultHostFilePath, DefaultDisabledHostFilePath);
            NativeMethods.FlushDns();
        }
    }

    /// <summary>
    /// Enable hosts file.
    /// </summary>
    public static void EnableHostsFile()
    {
        using (FileEx.DisableAttributes(DefaultDisabledHostFilePath, FileAttributes.ReadOnly))
        {
            File.Move(DefaultDisabledHostFilePath, DefaultHostFilePath);
            NativeMethods.FlushDns();
        }
    }

    /// <summary>
    /// Import specified hosts file into this hosts file.
    /// </summary>
    /// <param name="importFilePath">
    /// The import file path.
    /// </param>
    public void Import(string importFilePath)
    {
        if (filePath != importFilePath)
        {
            Entries.BatchUpdate(() =>
            {
                Entries.Clear();
                Entries.AddLines(File.ReadAllLines(importFilePath), RemoveDefaultText);
            });
        }
    }

    /// <summary>
    /// Archives the specified name.
    /// </summary>
    /// <param name="name">The name.</param>
    public void Archive(string name)
    {
        var archive = new HostsArchive(name);
        SaveAs(archive.FilePath);
        HostsArchiveList.Instance.Add(archive);
    }

    /// <summary>
    /// Restore to default OS hosts file.
    /// </summary>
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

    /// <summary>
    /// Save the hosts file changes to disk.
    /// </summary>
    public void Save()
    {
        SaveAs(filePath);
        NativeMethods.FlushDns();
    }

    /// <summary>
    /// Save the hosts file to the specified file.
    /// </summary>
    /// <param name="saveFilePath">
    /// The save file path.
    /// </param>
    public void SaveAs(string saveFilePath)
    {
        FileInfo info = new(saveFilePath);

        if (!Directory.Exists(info.DirectoryName))
        {
            Directory.CreateDirectory(info.DirectoryName!);
        }

        using (FileEx.DisableAttributes(saveFilePath, FileAttributes.ReadOnly))
        {
            File.WriteAllLines(
                saveFilePath,
                Entries.Select(entry => entry.UnparsedText));
        }
    }

    /// <summary>
    /// Refreshes to reflect current hosts file.
    /// </summary>
    /// <param name="removeDefault">
    /// if set to <c>true</c> remove default entries.
    /// </param>
    public void Refresh(bool removeDefault = true)
    {
        UndoManager.Instance.ClearHistory();

        Entries.BatchUpdate(() =>
        {
            Entries.Clear();
            Entries.AddLines(File.ReadAllLines(filePath), removeDefault);
        });

        NativeMethods.FlushDns();
    }

    /// <summary>
    /// Raise property changed event.
    /// </summary>
    /// <param name="property">
    /// The property.
    /// </param>
    /// <typeparam name="T">
    /// Type of object containing property.
    /// </typeparam>
    protected void OnPropertyChanged<T>(Expression<Func<T>> property)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property.GetPropertyName()));
    }

    /// <summary>
    /// Occurs when the hosts entries list changed.
    /// </summary>
    /// <param name="sender">
    /// The sender.
    /// </param>
    /// <param name="e">
    /// The event arguments.
    /// </param>
    private void OnHostsEntriesListChanged(object? sender, ListChangedEventArgs e)
    {
        OnPropertyChanged(() => LineCount);
        OnPropertyChanged(() => EnabledCount);
    }
}