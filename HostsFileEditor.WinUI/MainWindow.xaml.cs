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
            setRemoveEnabled: v => { RemoveButton?.IsEnabled = v; },
            setDuplicateEnabled: v => { DuplicateButton?.IsEnabled = v; },
            setMoveUpEnabled: v => { MoveUpButton?.IsEnabled = v; },
            setMoveDownEnabled: v => { MoveDownButton?.IsEnabled = v; },
            setToggleEnabled: v => { ToggleButton?.IsEnabled = v; },
            setCtxCopyVis: v => { CtxCopy?.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setCtxCutVis: v => { CtxCut?.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setCtxPasteVis: v => { CtxPaste?.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setCtxAddAboveVis: v => { CtxAddAbove?.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setCtxAddBelowVis: v => { CtxAddBelow?.Visibility = v ? Visibility.Visible : Visibility.Collapsed; },
            setUndoRedoVis: (undo, redo) =>
            {
                CtxUndo?.Visibility = undo ? Visibility.Visible : Visibility.Collapsed;

                CtxRedo?.Visibility = redo ? Visibility.Visible : Visibility.Collapsed;
            });

        // Apply persisted settings BEFORE the first HostsFile.Instance access (RefreshEntries
        // below): that access loads the hosts file and constructs every HostsEntry, so
        // RemoveDefaultText and AutoPingIPAddress must already be set or they are inert at
        // startup (header not stripped, no pings) until the next reload.
        IsPingIPs = LocalSettings.GetBool("AutoPingIPs", defaultValue: false);
        IsRemoveDefaultText = LocalSettings.GetBool("RemoveDefaultText", defaultValue: false);
        IsArchiveVisible = LocalSettings.GetBool("ArchiveVisible", defaultValue: false);
        HostsEntry.AutoPingIPAddress = IsPingIPs;
        HostsFile.RemoveDefaultText = IsRemoveDefaultText;
        ArchivesColumnWidth = IsArchiveVisible ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

        TrySetAppWindowTitleBar();
        TryEnableMicaBackdrop();
        RefreshEntries();
        RefreshArchives();

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

        // Subscribe to core entries changes so undo/redo of add/remove shows in UI
        HostsFile.Instance.Entries.ListChanged += OnCoreEntriesListChanged;

        // Unsubscribe when window closes to avoid leaks
        Closed += (s, e) =>
        {
            Utilities.UndoManager.Instance.HistoryChanged -= OnUndoHistoryChanged;
            HostsFile.Instance.Entries.ListChanged -= OnCoreEntriesListChanged;
        };
    }

    private void OnCoreEntriesListChanged(object? sender, ListChangedEventArgs e) =>
        // Use dispatcher to ensure UI-thread update and preserve selection when possible
        _ = DispatcherQueue.TryEnqueue(() => RefreshEntries(preserveSelection: true));

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

            // Warn about unsaved changes before the window closes (true exit; WinUI has no tray).
            appWindow.Closing += OnAppWindowClosing;
        }
    }

    // Set once the user has chosen to discard/save so the re-close isn't intercepted again.
    private bool _forceClose;

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_forceClose || !HostsFile.Instance.IsModified)
        {
            return;
        }

        // Cancel this close; re-close only after the user decides (the dialog is async).
        args.Cancel = true;

        var choice = await _dialogService.ShowThreeWayAsync(
            Content.XamlRoot,
            "Unsaved changes",
            "You have unsaved changes to the hosts file. Save them before exiting?",
            primaryText: "Save",
            secondaryText: "Don't save",
            closeText: "Cancel");

        if (choice == ContentDialogResult.None)
        {
            // Cancel: keep the window open.
            return;
        }

        if (choice == ContentDialogResult.Primary)
        {
            try
            {
                HostsFile.Instance.Save();
            }
            catch (Elevation.ElevationCancelledException)
            {
                // Declined the elevation prompt; keep the window open so changes aren't lost.
                return;
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Save failed", ex.Message);
                return;
            }
        }

        _forceClose = true;
        Close();
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
        var baseLeft = 4d;
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
            _backdropConfiguration?.IsInputActive = e.WindowActivationState != WindowActivationState.Deactivated;
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

    // Row-level accelerators matching the classic edition: Del, Ctrl+D, Alt+Up/Down,
    // Ctrl+Alt+Up/Down. Guarded so they don't hijack text editing in the filter box / cells.
    private void OnDeleteAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnDeleteClick(this, new RoutedEventArgs()), args);

    private void OnDuplicateAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnDuplicateClick(this, new RoutedEventArgs()), args);

    private void OnMoveUpAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnMoveUpClick(this, new RoutedEventArgs()), args);

    private void OnMoveDownAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnMoveDownClick(this, new RoutedEventArgs()), args);

    private void OnInsertAboveAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnInsertAboveClick(this, new RoutedEventArgs()), args);

    private void OnInsertBelowAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnInsertBelowClick(this, new RoutedEventArgs()), args);

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
            await ShowErrorDialogAsync("Error Importing Hosts File", $"An error occurred while importing the hosts file:\n\n{ex.Message}");
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            HostsFile.Instance.Save();
        }
        catch (Elevation.ElevationCancelledException)
        {
            // User declined the UAC prompt; nothing was written. Keep the unsaved edits.
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Saving Hosts File", $"An error occurred while saving the hosts file:\n\n{ex.Message}");
        }
    }

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
        // A newly added entry is blank (comment-only) and matches no active filter, so it would
        // be added invisibly and be uneditable. Clear filters first so the user can see and edit it.
        ClearAllFilters();

        var first = HostsFile.Instance.Entries.FirstOrDefault();
        if (first != null)
        {
            HostsFile.Instance.Entries.InsertBefore(first);
        }
        else
        {
            HostsFile.Instance.Entries.Add();
        }

        // RefreshEntries rebuilds the (filtered) view from the core list. Do not also
        // insert into the view directly: that adds the entry twice and ignores filters.
        RefreshEntries(true);
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
    }

    private void ClearAllFilters()
    {
        IsFilterCommentsHidden = false;
        IsFilterDisabledHidden = false;

        if (Content is FrameworkElement root && root.FindName("FilterTextBox") is TextBox ftb)
        {
            ftb.Text = string.Empty;
        }

        OnPropertyChanged(nameof(IsFilterCommentsHidden));
        OnPropertyChanged(nameof(IsFilterDisabledHidden));
        OnPropertyChanged(nameof(ActiveFilterCount));
        OnPropertyChanged(nameof(ActiveFiltersBadgeVisibility));
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        var selected = EntriesList.SelectedItems.Cast<HostsEntry>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        // Anchor on the entry immediately above the selection (in the visible/filtered
        // view), not on an entry within the selection itself: MoveBefore(x, x) is a no-op.
        var minIndex = selected.Min(Entries.IndexOf);
        if (minIndex <= 0)
        {
            return;
        }

        HostsFile.Instance.Entries.MoveBefore(selected, Entries[minIndex - 1]);
        RefreshEntries(true);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        var selected = EntriesList.SelectedItems.Cast<HostsEntry>().ToList();
        if (selected.Count == 0)
        {
            return;
        }

        // Anchor on the entry immediately below the selection (in the visible/filtered
        // view), not on an entry within the selection itself: MoveAfter(x, x) is a no-op.
        var maxIndex = selected.Max(Entries.IndexOf);
        if (maxIndex < 0 || maxIndex >= Entries.Count - 1)
        {
            return;
        }

        HostsFile.Instance.Entries.MoveAfter(selected, Entries[maxIndex + 1]);
        RefreshEntries(true);
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

    private async void OnDisableHostsClick(object sender, RoutedEventArgs e)
    {
        var isChecked = !HostsFile.IsEnabled; // current binding value
        try
        {
            if (!isChecked)
            {
                HostsFile.Instance.DisableHostsFile();
            }
            else
            {
                HostsFile.Instance.EnableHostsFile();
            }
        }
        catch (Elevation.ElevationCancelledException)
        {
            // User declined the UAC prompt; the file was not renamed.
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Changing Hosts File State", $"An error occurred while enabling or disabling the hosts file:\n\n{ex.Message}");
        }

        // Bound to HostsFile.IsEnabled (computed from the file system), so this reflects the
        // real post-operation state whether the toggle succeeded, was declined, or failed.
        OnPropertyChanged(nameof(IsDisabledHosts));
    }

    private void OnRefreshAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnRefreshClick(this, new RoutedEventArgs()), args);

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        var confirmed = await ShowConfirmationAsync("Reload hosts file?", "You will lose any unsaved changes. Continue?");
        if (!confirmed)
        {
            return;
        }

        try
        {
            // Honor the "remove default text" setting on reload, matching initial load and
            // Import; passing nothing would always strip the default hosts header.
            HostsFile.Instance.Refresh(HostsFile.RemoveDefaultText);
            RefreshEntries();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Reloading Hosts File", $"An error occurred while reloading the hosts file:\n\n{ex.Message}");
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

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var trimmed = name.Trim();

        // Reject invalid or duplicate names (matches the classic UI's InputForm). Without
        // this, Archive silently overwrites the existing file and adds a duplicate list row.
        if (!HostsArchive.Validate(new HostsArchive(trimmed).FilePath, out var error))
        {
            await ShowErrorDialogAsync("Invalid Archive Name", error);
            return;
        }

        try
        {
            HostsFile.Instance.Archive(trimmed);
            RefreshArchives();
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Creating Archive", $"An error occurred while creating the archive:\n\n{ex.Message}");
        }
    }

    private async void OnArchiveLoadClick(object sender, RoutedEventArgs e)
    {
        if (ArchiveList.SelectedItem is HostsArchive archive)
        {
            try
            {
                HostsFile.Instance.Import(archive.FilePath);
                RefreshEntries();
            }
            catch (Exception ex)
            {
                await ShowErrorDialogAsync("Error Loading Archive", $"An error occurred while loading the archive:\n\n{ex.Message}");
            }
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

    // Optimized to update Entries in-place to minimize ListView flicker.
    // Pseudocode:
    // 1. Capture filter text (trim).
    // 2. If preserveSelection: capture selected items into HashSet.
    // 3. Build filtered target list (newList) applying comment/disabled filters + text filter.
    // 4. Fast path: if counts equal AND all items in same order (reference equality), skip collection mutation.
    // 5. Otherwise perform minimal diff:
    //    For i from 0 .. newList.Count-1:
    //      a. If i >= Entries.Count -> Entries.Add(newItem)
    //      b. Else if Entries[i] != newItem:
    //           i.   Try find newItem in Entries at index j > i. If found -> move (remove at j, insert at i).
    //           ii.  Else insert newItem at i.
    //    After loop, remove any trailing items in Entries (while Entries.Count > newList.Count).
    // 6. Restore selection if preserveSelection: clear current selection and re-add items present in snapshot.
    //    If not preserving selection, emulate old behavior (clearing destroyed selection) by clearing selection explicitly.
    // 7. Raise property changed notifications for empty / filtered visibility (only if potentially changed).
    // 8. Update selection-dependent buttons & context menu.
    private void RefreshEntries(bool preserveSelection = false)
    {
        // 1. Filter text
        var text = string.Empty;
        if (Content is FrameworkElement root &&
            root.FindName("FilterTextBox") is TextBox ftb &&
            ftb.Text is string s)
        {
            text = s.Trim();
        }

        // 2. Capture selection if needed
        HashSet<HostsEntry>? selectedSnapshot = null;
        if (preserveSelection && EntriesList.SelectedItems.Count > 0)
        {
            selectedSnapshot = [.. EntriesList.SelectedItems.Cast<HostsEntry>()];
        }

        // 3. Build target filtered list
        var newList = new List<HostsEntry>();
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
                newList.Add(e);
            }
        }

        // 4. Fast path: identical sequence -> only adjust selection/properties
        var same =
            Entries.Count == newList.Count &&
            Entries.Zip(newList, ReferenceEquals).All(eq => eq);

        if (!same)
        {
            // 5. Minimal diff updates
            // Build index lookup for faster future searches if needed
            // (We rebuild on-the-fly because collection changes shift indices)
            for (var i = 0; i < newList.Count; i++)
            {
                var desired = newList[i];

                if (i >= Entries.Count)
                {
                    Entries.Add(desired);
                    continue;
                }

                if (!ReferenceEquals(Entries[i], desired))
                {
                    // Try to find desired later in the list to move it
                    var existingIndex = -1;
                    for (var j = i + 1; j < Entries.Count; j++)
                    {
                        if (ReferenceEquals(Entries[j], desired))
                        {
                            existingIndex = j;
                            break;
                        }
                    }

                    if (existingIndex >= 0)
                    {
                        if (existingIndex != i)
                        {
                            Entries.Move(existingIndex, i);
                        }
                    }
                    else
                    {
                        // Insert new item
                        Entries.Insert(i, desired);
                    }
                }
            }

            // Remove trailing excess
            while (Entries.Count > newList.Count)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
        }

        // 6. Restore / clear selection
        if (preserveSelection && selectedSnapshot is not null)
        {
            // Rebuild selection to reflect items still present
            var toSelect = Entries.Where(selectedSnapshot.Contains).ToList();

            // Avoid unnecessary churn if already matches
            var selectionDiffers =
                EntriesList.SelectedItems.Count != toSelect.Count ||
                EntriesList.SelectedItems.Cast<HostsEntry>().Except(toSelect).Any();

            if (selectionDiffers)
            {
                EntriesList.SelectedItems.Clear();
                foreach (var item in toSelect)
                {
                    EntriesList.SelectedItems.Add(item);
                }
            }
        }
        else
        {
            // Match original behavior (clearing collection previously removed selection)
            EntriesList.SelectedItems.Clear();
        }

        // 7. Property notifications (possible count / filter changes)
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));

        // 8. Update dependent UI
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
            // Restore the resting underline using a theme-aware brush (was hardcoded
            // white, which is invisible on light themes).
            underline.Background = Application.Current.Resources["ControlStrokeColorDefaultBrush"] as Brush ?? new SolidColorBrush(Colors.Gray);
        }
    }

    private void OnPingIPsClick(object sender, RoutedEventArgs e) =>
        ApplyToggleSetting("AutoPingIPs", v => { IsPingIPs = v; HostsEntry.AutoPingIPAddress = v; }, nameof(IsPingIPs), sender);

    private void OnRemoveDefaultTextClick(object sender, RoutedEventArgs e) =>
        ApplyToggleSetting("RemoveDefaultText", v => { IsRemoveDefaultText = v; HostsFile.RemoveDefaultText = v; }, nameof(IsRemoveDefaultText), sender);

    private void OnUndoHistoryChanged(object? sender, EventArgs e) =>
        _ = DispatcherQueue.TryEnqueue(_selectionService.UpdateContextMenuItems);

    private void OnEntriesSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
    }

    private void OnArchiveSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ArchiveList is not null)
        {
            var hasSelection = ArchiveList.SelectedItem is HostsArchive;
            ArchiveLoadButton?.IsEnabled = hasSelection;

            ArchiveDeleteButton?.IsEnabled = hasSelection;
        }
    }
}
