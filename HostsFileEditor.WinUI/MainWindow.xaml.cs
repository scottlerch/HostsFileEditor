using HostsFileEditor.Services;
using HostsFileEditor.Win32;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WinRT;
using WinRT.Interop;

namespace HostsFileEditor;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    internal ObservableCollection<HostsEntry> Entries { get; } = [];

    internal ObservableCollection<HostsArchive> Archives { get; } = [];

    private IEnumerable<HostsEntry>? _clipboardEntries;

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

    public bool IsFilterCommentsHidden { get; private set; }

    public bool IsFilterDisabledHidden { get; private set; }

    public int ActiveFilterCount => (IsFilterCommentsHidden ? 1 : 0) + (IsFilterDisabledHidden ? 1 : 0);

    public Visibility ActiveFiltersBadgeVisibility => ActiveFilterCount > 0 ? Visibility.Visible : Visibility.Collapsed;

    private MicaController? _micaController;
    private SystemBackdropConfiguration? _backdropConfiguration;
    private Grid? _titleBarHost;

    private bool _isAnimatingArchive; // prevent re-entrant animations

    private readonly DialogService _dialogService;
    private readonly AnimationService _animationService;
    private readonly SelectionStateService _selectionService;

    public MainWindow(DialogService dialogService, AnimationService animationService)
    {
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _animationService = animationService ?? throw new ArgumentNullException(nameof(animationService));

        InitializeComponent();

        _selectionService = new SelectionStateService(
            hasSelection: () => EntriesList is not null && EntriesList.SelectedItems.Count > 0,
            setRemoveEnabled: v => { if (RemoveButton is not null) RemoveButton.IsEnabled = v; },
            setDuplicateEnabled: v => { if (DuplicateButton is not null) DuplicateButton.IsEnabled = v; },
            setMoveUpEnabled: v => { if (MoveUpButton is not null) MoveUpButton.IsEnabled = v; },
            setMoveDownEnabled: v => { if (MoveDownButton is not null) MoveDownButton.IsEnabled = v; },
            setToggleEnabled: v => { if (ToggleButton is not null) ToggleButton.IsEnabled = v; },
            setCtxCopyVis: v => { if (CtxCopy is not null) CtxCopy.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setCtxCutVis: v => { if (CtxCut is not null) CtxCut.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setCtxPasteVis: v => { if (CtxPaste is not null) CtxPaste.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setCtxAddAboveVis: v => { if (CtxAddAbove is not null) CtxAddAbove.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setCtxAddBelowVis: v => { if (CtxAddBelow is not null) CtxAddBelow.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setUndoRedoVis: (undo, redo) =>
            {
                if (CtxUndo is not null) CtxUndo.Visibility = undo ? Visibility.Visible : Visibility.Collapsed;
                if (CtxRedo is not null) CtxRedo.Visibility = redo ? Visibility.Visible : Visibility.Collapsed;
            });

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

        // Ensure buttons reflect current selection/state at startup
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();

        // Subscribe to undo history changes so we can update Undo/Redo visibility
        Utilities.UndoManager.Instance.HistoryChanged += OnUndoHistoryChanged;

        // Unsubscribe when window closes to avoid leaks
        Closed += (s, e) => Utilities.UndoManager.Instance.HistoryChanged -= OnUndoHistoryChanged;
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

    private void OnCopyAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnCopyClick(this, new RoutedEventArgs()), args);

    private void OnCutAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnCutClick(this, new RoutedEventArgs()), args);

    private void OnPasteAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnPasteClick(this, new RoutedEventArgs()), args);

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Win32FileDialogs.OpenFileDialog(GetHwnd(), "All Files (*.*)|*.*");
            if (!string.IsNullOrWhiteSpace(path))
            {
                HostsFile.Instance.Import(path);
                RefreshEntries();
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Saving Hosts File", $"An error occurred while saving the hosts file:\n\n{ex.Message}");
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => HostsFile.Instance.Save();

    private void OnSaveAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnSaveClick(this, new RoutedEventArgs()), args);

    private async void OnSaveAsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Win32FileDialogs.SaveFileDialog(GetHwnd(), "hosts", "Hosts (*.txt;*.hosts)|*.txt;*.hosts|Text (*.txt)|*.txt|All Files (*.*)|*.*");
            if (!string.IsNullOrWhiteSpace(path))
            {
                HostsFile.Instance.SaveAs(path);
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Saving Hosts File", $"An error occurred while saving the hosts file:\n\n{ex.Message}");
        }
    }

    private void OnInsertBelowClick(object sender, RoutedEventArgs e)
    {
        var current = EntriesList.SelectedItems.Cast<HostsEntry>().FirstOrDefault();
        if (current != null)
        {
            HostsFile.Instance.Entries.InsertAfter(current);
            RefreshEntries(true);
        }
    }

    private void OnInsertAboveClick(object sender, RoutedEventArgs e)
    {
        var current = EntriesList.SelectedItems.Cast<HostsEntry>().FirstOrDefault();
        if (current != null)
        {
            HostsFile.Instance.Entries.InsertBefore(current);
            RefreshEntries(true);
        }
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

        // Update UI buttons when selection changes cause deletion
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
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

        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
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

        _selectionService.UpdateContextMenuItems();
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

    private void OnRefreshAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnRefreshClick(this, new RoutedEventArgs()), args);

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        var confirmed = await ShowConfirmationAsync("Reload hosts file?", "You will lose any unsaved changes. Continue?");
        if (confirmed)
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

    private void OnOpenInTextEditorClick(object sender, RoutedEventArgs e) => Utilities.FileOpener.OpenTextFile(HostsFile.DefaultHostFilePath);

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e) => RefreshEntries(true);

    private void OnFilterCommentsClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem t)
        {
            // IsChecked == true => hide comments
            IsFilterCommentsHidden = t.IsChecked == true;
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
            IsFilterDisabledHidden = t.IsChecked == true;
            RefreshEntries(true);
            OnPropertyChanged(nameof(IsFilterDisabledHidden));
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(ActiveFiltersBadgeVisibility));
        }
    }

    private void OnResetFiltersClick(object sender, RoutedEventArgs e)
    {
        var changed = IsFilterCommentsHidden || IsFilterDisabledHidden;
        IsFilterCommentsHidden = false;
        IsFilterDisabledHidden = false;
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
        if (IsTextBoxFocused())
        {
            return;
        }

        Utilities.UndoManager.Instance.Undo();
        args.Handled = true;
    }

    private void OnRedoAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextBoxFocused())
        {
            return;
        }

        Utilities.UndoManager.Instance.Redo();
        args.Handled = true;
    }

    private async void OnArchiveClick(object sender, RoutedEventArgs e)
    {
        var name = _dialogService is not null
            ? await _dialogService.ShowInputAsync(Content.XamlRoot, "Create Archive", "Archive name", "OK", "Cancel")
            : null;

        if (!string.IsNullOrWhiteSpace(name))
        {
            HostsFile.Instance.Archive(name.Trim());
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

    private async void OnViewArchiveClick(object sender, RoutedEventArgs e)
    {
        var show = sender is AppBarToggleButton { IsChecked: true };
        await ToggleArchiveVisibilityAsync(show);
    }

    private async void OnBackClick(object sender, RoutedEventArgs e) => await ToggleArchiveVisibilityAsync(false);

    private void RefreshEntries(bool preserveSelection = false)
    {
        var text = string.Empty;
        if (Content is FrameworkElement root && root.FindName("FilterTextBox") is TextBox ftb && ftb.Text is string s)
        {
            text = s.Trim();
        }
        var sel = preserveSelection ? EntriesList.SelectedItems.Cast<HostsEntry>().ToHashSet() : [];

        Entries.Clear();
        foreach (var e in HostsFile.Instance.Entries)
        {
            if (IsFilterCommentsHidden && e.HasCommentOnly)
            {
                continue;
            }
            if (IsFilterDisabledHidden && !e.Enabled && !e.HasCommentOnly)
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

        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));

        // Keep selection-aware buttons in sync after refresh
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
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

    private void OnPingIPsClick(object sender, RoutedEventArgs e) =>
        ApplyToggleSetting("AutoPingIPs", v => { IsPingIPs = v; HostsEntry.AutoPingIPAddress = v; }, nameof(IsPingIPs), sender);

    private void OnRemoveDefaultTextClick(object sender, RoutedEventArgs e) =>
        ApplyToggleSetting("RemoveDefaultText", v => { IsRemoveDefaultText = v; HostsFile.RemoveDefaultText = v; }, nameof(IsRemoveDefaultText), sender);

    private void OnUndoHistoryChanged(object? sender, EventArgs e) =>
        _ = DispatcherQueue.TryEnqueue(() => _selectionService.UpdateContextMenuItems());

    private void OnEntriesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
    }
}
