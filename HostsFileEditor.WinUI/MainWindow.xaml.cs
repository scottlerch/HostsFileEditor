using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Windows.Storage.Pickers;
using WinRT;
using WinRT.Interop;

namespace HostsFileEditor;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    // Note: do not define a member named 'Bindings' — generated partial class provides that field.

    internal ObservableCollection<HostsEntry> Entries { get; } = [];

    internal ObservableCollection<HostsArchive> Archives { get; } = [];

    private IEnumerable<HostsEntry>? _clipboardEntries;

    // Internal flags: true means the corresponding entries are HIDDEN (filtered out)
    private bool _filterComments; // hide comment-only lines
    private bool _filterDisabled; // hide disabled entries

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsDisabledHosts => !HostsFile.IsEnabled;
    public bool IsPingIPs { get; private set; }
    public bool IsRemoveDefaultText { get; private set; }
    public GridLength ArchivesColumnWidth { get; private set; } = new(0);
    public bool IsArchiveVisible { get; private set; }
    public bool IsBackEnabled => IsArchiveVisible;

    public Visibility ArchivesEmptyVisibility => Archives.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility MainViewVisibility => IsArchiveVisible ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ArchiveViewVisibility => IsArchiveVisible ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EntriesEmptyVisibility => HostsFile.Instance.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EntriesFilteredVisibility => HostsFile.Instance.Entries.Count > 0 && Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    // New public properties matching new XAML wording (checked == hide)
    public bool IsFilterCommentsHidden => _filterComments;   // bound to ToggleMenuFlyoutItem.IsChecked
    public bool IsFilterDisabledHidden => _filterDisabled;   // bound to ToggleMenuFlyoutItem.IsChecked

    public int ActiveFilterCount => (_filterComments ? 1 : 0) + (_filterDisabled ? 1 : 0);
    public Visibility ActiveFiltersBadgeVisibility => ActiveFilterCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    private MicaController? _micaController;
    private SystemBackdropConfiguration? _backdropConfiguration;
    private Grid? _titleBarHost;

    public MainWindow()
    {
        InitializeComponent();
        TrySetAppWindowTitleBar();
        TryEnableMicaBackdrop();
        RefreshEntries();
        RefreshArchives();

        IsPingIPs = LocalSettings.GetBool("AutoPingIPs", defaultValue: false);
        IsRemoveDefaultText = LocalSettings.GetBool("RemoveDefaultText", defaultValue: false);
        IsArchiveVisible = LocalSettings.GetBool("ArchiveVisible", defaultValue: false);
        HostsEntry.AutoPingIPAddress = IsPingIPs;
        HostsFile.RemoveDefaultText = IsRemoveDefaultText;
        ArchivesColumnWidth = IsArchiveVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        OnPropertyChanged(nameof(IsBackEnabled));
        OnPropertyChanged(nameof(MainViewVisibility));
        OnPropertyChanged(nameof(ArchiveViewVisibility));
        OnPropertyChanged(nameof(ArchivesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
        OnPropertyChanged(nameof(IsFilterCommentsHidden));
        OnPropertyChanged(nameof(IsFilterDisabledHidden));
        OnPropertyChanged(nameof(ActiveFilterCount));
        OnPropertyChanged(nameof(ActiveFiltersBadgeVisibility));
    }

    // Handlers invoked by KeyboardAccelerators in XAML (names must match generated wiring)
    private void OnCopyAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Allow textboxes to handle their own copy
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        OnCopyClick(this, new RoutedEventArgs());
        args.Handled = true;
    }

    private void OnCutAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        OnCutClick(this, new RoutedEventArgs());
        args.Handled = true;
    }

    private void OnPasteAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        OnPasteClick(this, new RoutedEventArgs());
        args.Handled = true;
    }

    private void OnCopyClick(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        // Forward to existing handler
        OnCopyClick(this, new RoutedEventArgs());
        args.Handled = true;
    }

    private void OnCutClick(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        OnCutClick(this, new RoutedEventArgs());
        args.Handled = true;
    }

    private void OnPasteClick(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        OnPasteClick(this, new RoutedEventArgs());
        args.Handled = true;
    }

    private void TrySetAppWindowTitleBar()
    {
        var hwnd = GetHwnd();
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Extend content and use custom title bar element from XAML
        ExtendsContentIntoTitleBar = true;
        if (Content is FrameworkElement root && root.FindName("AppTitleBar") is Grid fe)
        {
            _titleBarHost = fe;
            SetTitleBar(fe);
        }
        if (appWindow is not null)
        {
            appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            // Keep content clear of the caption buttons area
            appWindow.Changed += (_, __) => UpdateTitleBarPadding(appWindow);
            SizeChanged += (_, __) => UpdateTitleBarPadding(appWindow);
            UpdateTitleBarPadding(appWindow);
        }
    }

    private void UpdateTitleBarPadding(AppWindow appWindow)
    {
        if (_titleBarHost is null)
        {
            return;
        }

        // Insets provided by the system for areas occupied by caption buttons and drag region.
        var leftInset = appWindow.TitleBar.LeftInset;
        var rightInset = appWindow.TitleBar.RightInset;

        // Keep some horizontal breathing room and avoid overlap with caption buttons.
        var baseLeft = 12d;
        var baseRight = 12d;
        _titleBarHost.Padding = new Thickness(baseLeft + leftInset, 0, baseRight + rightInset, 0);
    }

    private bool TryEnableMicaBackdrop()
    {
        if (!MicaController.IsSupported())
        {
            return false;
        }

        _backdropConfiguration = new SystemBackdropConfiguration
        {
            IsInputActive = true,
            Theme = SystemBackdropTheme.Default
        };

        _micaController = new MicaController { Kind = MicaKind.BaseAlt };
        _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_backdropConfiguration);

        Activated += (s, e) =>
        {
            if (_backdropConfiguration is not null)
            {
                _backdropConfiguration.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
            }
        };
        Closed += (s, e) =>
        {
            _micaController?.Dispose();
            _micaController = null;
            _backdropConfiguration = null;
        };

        return true;
    }

    private IntPtr GetHwnd() => WindowNative.GetWindowHandle(this);

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, GetHwnd());
        picker.FileTypeFilter.Clear();
        picker.FileTypeFilter.Add("*");
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
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
    }

    private void OnAddToTop(object sender, RoutedEventArgs e)
    {
        var first = HostsFile.Instance.Entries.FirstOrDefault();
        if (first != null)
        {
            HostsFile.Instance.Entries.InsertBefore(first);
        }
        else
        {
            HostsFile.Instance.Entries.Add();
        }

        Entries.Insert(0, HostsFile.Instance.Entries.First());
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
    }

    private void OnInsertAboveClick(object sender, RoutedEventArgs e)
    {
        var current = EntriesList.SelectedItems.Cast<HostsEntry>().FirstOrDefault();
        if (current != null)
        {
            HostsFile.Instance.Entries.InsertBefore(current);
            RefreshEntries(true);
        }
        else
        {
            OnInsertBelowClick(sender, e);
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
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var rows = EntriesList.SelectedItems.Cast<HostsEntry>().ToList();
        if (rows.Count > 0)
        {
            _clipboardEntries = [.. rows.Select(r => new HostsEntry(r))];
        }
    }

    private void OnCutClick(object sender, RoutedEventArgs e)
    {
        var rows = EntriesList.SelectedItems.Cast<HostsEntry>().ToList();
        if (rows.Count > 0)
        {
            _clipboardEntries = [.. rows.Select(r => new HostsEntry(r))];
            HostsFile.Instance.Entries.Remove(rows);
            foreach (var r in rows)
            {
                Entries.Remove(r);
            }
        }
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
    }

    private void OnPasteClick(object sender, RoutedEventArgs e)
    {
        if (_clipboardEntries != null && EntriesList.SelectedItems.Count > 0)
        {
            var current = (HostsEntry)EntriesList.SelectedItem;
            HostsFile.Instance.Entries.Insert(current, _clipboardEntries);
            RefreshEntries();
            _clipboardEntries = null;
        }
    }

    private void OnDuplicateClick(object sender, RoutedEventArgs e)
    {
        var rows = EntriesList.SelectedItems.Cast<HostsEntry>().ToList();
        if (rows.Count > 0)
        {
            foreach (var entry in rows)
            {
                HostsFile.Instance.Entries.InsertAfter(entry, new HostsEntry(entry));
            }
            RefreshEntries(true);
        }
        else if (EntriesList.SelectedItem is HostsEntry current)
        {
            HostsFile.Instance.Entries.InsertAfter(current, new HostsEntry(current));
            RefreshEntries(true);
        }
    }

    private void OnDisableHostsClick(object sender, RoutedEventArgs e)
    {
        var isChecked = !HostsFile.IsEnabled; // current binding value
        if (!isChecked)
        {
            HostsFile.DisableHostsFile();
        }
        else
        {
            HostsFile.EnableHostsFile();
        }

        OnPropertyChanged(nameof(IsDisabledHosts));
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        var dlg = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Reload hosts file?",
            Content = "You will lose any unsaved changes. Continue?",
            PrimaryButtonText = "Yes",
            CloseButtonText = "No"
        };
        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            HostsFile.Instance.Refresh();
            RefreshEntries();
        }
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        HostsFile.Instance.RestoreDefault();
        RefreshEntries();
    }

    private void OnOpenTextEditorClick(object sender, RoutedEventArgs e) => Utilities.FileOpener.OpenTextFile(HostsFile.DefaultHostFilePath);

    // Added to match XAML wiring: forwards to existing handler
    private void OnOpenInTextEditorClick(object sender, RoutedEventArgs e) => OnOpenTextEditorClick(sender, e);

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e) => RefreshEntries(true);

    private void OnFilterCommentsClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem t)
        {
            // IsChecked == true => hide comments
            _filterComments = t.IsChecked == true;
            RefreshEntries(true);
            OnPropertyChanged(nameof(IsFilterCommentsHidden));
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(ActiveFiltersBadgeVisibility));
        }
    }

    private void OnFilterDisabledClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem t)
        {
            // IsChecked == true => hide disabled entries
            _filterDisabled = t.IsChecked == true;
            RefreshEntries(true);
            OnPropertyChanged(nameof(IsFilterDisabledHidden));
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(ActiveFiltersBadgeVisibility));
        }
    }

    private void OnResetFiltersClick(object sender, RoutedEventArgs e)
    {
        bool changed = _filterComments || _filterDisabled;
        _filterComments = false;
        _filterDisabled = false;
        if (changed)
        {
            RefreshEntries(true);
            OnPropertyChanged(nameof(IsFilterCommentsHidden));
            OnPropertyChanged(nameof(IsFilterDisabledHidden));
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(ActiveFiltersBadgeVisibility));
        }
    }

    private void OnCheckClick(object sender, RoutedEventArgs e)
    {
        var rows = EntriesList.SelectedItems.Cast<HostsEntry>().ToList();
        if (rows.Count > 0)
        {
            // If all selected are enabled, then uncheck; otherwise check
            var allEnabled = rows.All(r => r.Enabled);
            HostsFile.Instance.Entries.SetEnabled(rows, isEnabled: !allEnabled);
        }
        else if (EntriesList.SelectedItem is HostsEntry current)
        {
            // Toggle single selection
            HostsFile.Instance.Entries.SetEnabled([current], isEnabled: !current.Enabled);
        }
    }

    private void OnUndoClick(object sender, RoutedEventArgs e) => Utilities.UndoManager.Instance.Undo();

    private void OnRedoClick(object sender, RoutedEventArgs e) => Utilities.UndoManager.Instance.Redo();

    private void OnUndoAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Avoid interfering when typing in a TextBox
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        Utilities.UndoManager.Instance.Undo();
        args.Handled = true;
    }

    private void OnRedoAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (FocusManager.GetFocusedElement() is TextBox)
        {
            return;
        }

        Utilities.UndoManager.Instance.Redo();
        args.Handled = true;
    }

    private async void OnArchiveClick(object sender, RoutedEventArgs e)
    {
        var input = new TextBox { PlaceholderText = "Archive name" };
        var dlg = new ContentDialog
        {
            XamlRoot = Content.XamlRoot,
            Title = "Create Archive",
            Content = input,
            PrimaryButtonText = "OK",
            CloseButtonText = "Cancel"
        };
        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text))
        {
            HostsFile.Instance.Archive(input.Text.Trim());
            RefreshArchives();
        }
    }

    private void OnArchiveLoadClick(object sender, RoutedEventArgs e)
    {
        if (ArchiveList.SelectedItem is HostsArchive archive)
        {
            HostsFile.Instance.Import(archive.FilePath);
            RefreshEntries();
        }
    }

    private void OnArchiveDeleteClick(object sender, RoutedEventArgs e)
    {
        if (ArchiveList.SelectedItem is HostsArchive archive)
        {
            HostsArchiveList.Instance.Delete(archive);
            RefreshArchives();
        }
    }

    private void OnViewArchiveClick(object sender, RoutedEventArgs e)
    {
        var isChecked = sender is AppBarToggleButton { IsChecked: true };
        IsArchiveVisible = isChecked;
        ArchivesColumnWidth = isChecked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        LocalSettings.SetBool("ArchiveVisible", isChecked);
        OnPropertyChanged(nameof(IsArchiveVisible));
        OnPropertyChanged(nameof(ArchivesColumnWidth));
        OnPropertyChanged(nameof(IsBackEnabled));
        OnPropertyChanged(nameof(MainViewVisibility));
        OnPropertyChanged(nameof(ArchiveViewVisibility));
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (!IsArchiveVisible)
        {
            return;
        }

        IsArchiveVisible = false;
        LocalSettings.SetBool("ArchiveVisible", false);
        OnPropertyChanged(nameof(IsArchiveVisible));
        OnPropertyChanged(nameof(IsBackEnabled));
        OnPropertyChanged(nameof(MainViewVisibility));
        OnPropertyChanged(nameof(ArchiveViewVisibility));
    }

    private void OnPingIPsClick(object sender, RoutedEventArgs e)
    {
        var isChecked = sender is AppBarToggleButton { IsChecked: true };
        IsPingIPs = isChecked;
        HostsEntry.AutoPingIPAddress = IsPingIPs;
        LocalSettings.SetBool("AutoPingIPs", IsPingIPs);
        OnPropertyChanged(nameof(IsPingIPs));
    }

    private void OnRemoveDefaultTextClick(object sender, RoutedEventArgs e)
    {
        var isChecked = sender is AppBarToggleButton { IsChecked: true };
        IsRemoveDefaultText = isChecked;
        HostsFile.RemoveDefaultText = IsRemoveDefaultText;
        LocalSettings.SetBool("RemoveDefaultText", IsRemoveDefaultText);
        OnPropertyChanged(nameof(IsRemoveDefaultText));
    }

    private void RefreshEntries(bool preserveSelection = false)
    {
        var text = FilterTextBox.Text?.Trim() ?? string.Empty;
        var sel = preserveSelection ? EntriesList.SelectedItems.Cast<HostsEntry>().ToHashSet() : [];

        Entries.Clear();
        foreach (var e in HostsFile.Instance.Entries)
        {
            if (_filterComments && e.HasCommentOnly)
            {
                continue;
            }
            if (_filterDisabled && !e.Enabled && !e.HasCommentOnly)
            {
                continue;
            }

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

        // Notify visibility changes for entry-related messages
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
    }

    private void RefreshArchives()
    {
        Archives.Clear();
        foreach (var a in HostsArchiveList.Instance)
        {
            Archives.Add(a);
        }
        OnPropertyChanged(nameof(ArchivesEmptyVisibility));
    }

    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    // Underline focus visuals for filter text box
    private void OnFilterBoxGotFocus(object sender, RoutedEventArgs e)
    {
        if (Content is FrameworkElement root && root.FindName("FilterUnderline") is Border underline)
        {
            underline.Background = Application.Current.Resources["AccentFillColorDefaultBrush"] as Brush ?? new SolidColorBrush(Colors.DodgerBlue);
        }
    }

    private void OnFilterBoxLostFocus(object sender, RoutedEventArgs e)
    {
        if (Content is FrameworkElement root && root.FindName("FilterUnderline") is Border underline)
        {
            underline.Background = new SolidColorBrush(Colors.White);
        }
    }
}
