using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.ObjectModel;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HostsFileEditor;

public sealed partial class MainWindow : Window
{
    internal ObservableCollection<HostsEntry> Entries { get; } = [];

    internal ObservableCollection<HostsArchive> Archives { get; } = [];

    public MainWindow()
    {
        InitializeComponent();

        // Bind to current HostsFile
        foreach (var e in HostsFile.Instance.Entries)
        {
            Entries.Add(e);
        }

        foreach (var a in HostsArchiveList.Instance)
        {
            Archives.Add(a);
        }

        DisableHostsToggle.IsChecked = !HostsFile.IsEnabled;
    }

    private IntPtr GetHwnd() => WindowNative.GetWindowHandle(this);

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, GetHwnd());
        picker.FileTypeFilter.Add("*.*");
        var file = await picker.PickSingleFileAsync();
        if (file != null)
        {
            HostsFile.Instance.Import(file.Path);
            RefreshEntries();
        }
    }

    private async void OnSaveAsClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, GetHwnd());
        picker.FileTypeChoices.Add("Hosts", [".txt", ".hosts"]);
        picker.SuggestedFileName = "hosts";
        var file = await picker.PickSaveFileAsync();
        if (file != null)
        {
            HostsFile.Instance.SaveAs(file.Path);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => HostsFile.Instance.Save();

    private void OnInsertBelowClick(object sender, RoutedEventArgs e)
    {
        HostsFile.Instance.Entries.Add();
        if (HostsFile.Instance.Entries.LastOrDefault() is { } last && !Entries.Contains(last))
        {
            Entries.Add(last);
        }
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItems.Count > 0 && EntriesList.SelectedItem is HostsEntry lastSel)
        {
            HostsFile.Instance.Entries.MoveBefore(EntriesList.SelectedItems.Cast<HostsEntry>(), lastSel);
            RefreshEntries(true);
        }
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItems.Count > 0 && EntriesList.SelectedItem is HostsEntry firstSel)
        {
            HostsFile.Instance.Entries.MoveAfter(EntriesList.SelectedItems.Cast<HostsEntry>(), firstSel);
            RefreshEntries(true);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var items = EntriesList.SelectedItems.Cast<HostsEntry>().ToList();
        if (items.Count > 0)
        {
            HostsFile.Instance.Entries.Remove(items);
            foreach (var i in items)
            {
                Entries.Remove(i);
            }
        }
    }

    private void OnDisableHostsClick(object sender, RoutedEventArgs e)
    {
        var isChecked = DisableHostsToggle.IsChecked == true;
        if (isChecked)
        {
            HostsFile.DisableHostsFile();
        }
        else
        {
            HostsFile.EnableHostsFile();
        }

        DisableHostsToggle.IsChecked = !HostsFile.IsEnabled;
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        HostsFile.Instance.RestoreDefault();
        RefreshEntries();
    }

    private void OnOpenTextEditorClick(object sender, RoutedEventArgs e) => Utilities.FileOpener.OpenTextFile(HostsFile.DefaultHostFilePath);

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e) => RefreshEntries();

    private void RefreshEntries(bool preserveSelection = false)
    {
        var text = FilterTextBox.Text?.Trim() ?? string.Empty;
        var sel = preserveSelection ? EntriesList.SelectedItems.Cast<HostsEntry>().ToHashSet() : [];

        Entries.Clear();
        foreach (var e in HostsFile.Instance.Entries)
        {
            if (string.IsNullOrEmpty(text) || e.ToString().Contains(text, StringComparison.OrdinalIgnoreCase))
            {
                Entries.Add(e);
            }
        }

        if (preserveSelection)
        {
            foreach (var e in Entries.Where(sel.Contains))
            {
                EntriesList.SelectedItems.Add(e);
            }
        }
    }
}
