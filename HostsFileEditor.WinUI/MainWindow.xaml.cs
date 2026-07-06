using HostsFileEditor.Services;
using HostsFileEditor.Win32;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Windows.System;
using WinRT;
using WinRT.Interop;

namespace HostsFileEditor;

public sealed partial class MainWindow : Window, INotifyPropertyChanged
{
    // Replaceable so the initial load can bulk-assign the whole (filtered) set in one rebind
    // instead of adding hundreds of thousands of items one at a time.
    internal ObservableCollection<HostsEntry> Entries { get; private set; } = [];

    // False until the (potentially slow) initial hosts-file parse has completed on a background
    // thread. Gates every property that touches HostsFile.Instance.Entries so the eager x:Bind
    // evaluation during InitializeComponent doesn't trigger that parse on the UI thread.
    private bool _isLoaded;

    // Set when the initial hosts-file load failed. The Lazy<HostsFile> has CACHED that exception —
    // every later HostsFile.Instance touch rethrows it — so the visibility getters and handlers must
    // short-circuit on this flag instead of dereferencing Instance (which would crash the app from
    // inside a binding refresh or an event handler).
    private bool _loadFailed;

    // Set while an explicit handler mutates the Core list and refreshes the view itself, so the
    // ListChanged subscription below doesn't also fire an (O(n^2)) minimal-diff refresh on top.
    // volatile: a Core ListChanged can arrive off the UI thread (an auto-ping result whose property
    // change wasn't marshalled — see HostsEntry.PingAsync), so OnCoreEntriesListChanged must read the
    // freshest value rather than a cached one.
    private volatile bool _suspendCoreListSync;

    // Guards against re-enqueuing the selection-state refresh while one is already pending, so a
    // burst of SelectionChanged events (e.g. Ctrl+A over a large list) collapses to one update.
    private bool _selectionUpdatePending;

    // Coalesce Core ListChanged bursts (e.g. hundreds of thousands of auto-ping results) into one
    // queued view refresh per dispatcher pass; _pendingStructuralRefresh remembers whether any event
    // in the burst was structural (Reset/ItemAdded/ItemDeleted), which upgrades the queued refresh to
    // the bulk rebind. volatile: ListChanged can arrive off the UI thread (see _suspendCoreListSync).
    private volatile bool _coreRefreshPending;
    private volatile bool _pendingStructuralRefresh;

    // Our own mirror of the ListView selection, maintained from the cheap SelectionChanged deltas.
    // Reading ListView.SelectedItems (its Count or enumeration) is O(n^2) for a large selection and
    // hung the app for minutes on Select-All + Remove/Cut, so the bulk handlers use THIS instead and
    // never touch SelectedItems.
    private readonly HashSet<HostsEntry> _selectedEntries = [];

    // Set while a bulk op rebinds the view, to skip delta-tracking of the resulting teardown churn
    // (the mirror set is reset explicitly instead). volatile for the same cross-thread reason as
    // _suspendCoreListSync.
    private volatile bool _suspendSelectionTracking;

    // "Select all" is tracked logically above this many rows instead of populating the native
    // ListView selection. Reason: WinUI's ListView.SelectedItems clears one item at a time, so
    // tearing down a huge native selection (on delete/cut/filter) is O(n^2) and hangs for minutes.
    // Below the threshold, native selection is used (its teardown is cheap and gives row highlight).
    private const int LogicalSelectAllThreshold = 20_000;

    // True when Ctrl+A selected "all" logically (native selection left empty). GetSelectedEntries
    // resolves it to the full Entries list; any real selection change clears it. Mutate only via
    // SetLogicalSelectAll so the banner (LogicalSelectAllVisibility) stays in sync.
    private bool _logicalSelectAll;

    // Since a logical Select-All shows no row highlight, surface a banner so the user can see that
    // "all" is selected (otherwise a following Delete/Cut is a surprise).
    public Visibility LogicalSelectAllVisibility => _logicalSelectAll ? Visibility.Visible : Visibility.Collapsed;

    public string SelectAllBannerText => $"All {Entries.Count:N0} entries selected — press Esc or click a row to clear.";

    public Visibility LoadingVisibility => _isLoaded || _loadFailed ? Visibility.Collapsed : Visibility.Visible;

    // Bottom status bar text: total lines and host (enabled) entries, mirroring the classic edition's
    // status strip (HostsFile.LineCount / EnabledCount). Recomputed on each view refresh; see
    // UpdateStatusCounts.
    private string _statusText = string.Empty;

    public string StatusText => _statusText;

    // Dev/test HFE_HOSTS_PATH override indicator for the title bar (OneTime — set at startup).
    public string OverrideIndicatorText => HostsFile.OverridePath is { } p ? $"[{p}]" : string.Empty;

    public Visibility OverrideIndicatorVisibility => HostsFile.OverridePath is not null ? Visibility.Visible : Visibility.Collapsed;

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

    // Both getters check _loadFailed BEFORE dereferencing HostsFile.Instance — see _loadFailed.
    public Visibility EntriesEmptyVisibility =>
        _loadFailed || (_isLoaded && HostsFile.Instance.Entries.Count == 0) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EntriesFilteredVisibility =>
        !_loadFailed && _isLoaded && HostsFile.Instance.Entries.Count > 0 && Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
            hasSelection: () => _logicalSelectAll || _selectedEntries.Count > 0,
            hasAnchoredSelection: () => _selectedEntries.Count > 0,
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

        // Auto-pings started during the off-UI-thread parse capture a null SynchronizationContext;
        // register the UI context so their failure notifications marshal here instead of updating
        // x:Bind-bound rows from the thread pool (RPC_E_WRONG_THREAD).
        HostsEntry.UiSynchronizationContext = SynchronizationContext.Current;

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

        // Unsubscribe when window closes to avoid leaks
        Closed += (s, e) =>
        {
            Utilities.UndoManager.Instance.HistoryChanged -= OnUndoHistoryChanged;
            if (_isLoaded)
            {
                HostsFile.Instance.Entries.ListChanged -= OnCoreEntriesListChanged;
            }
        };

        // Parse the hosts file on a background thread (it can be very large) and then bulk-populate
        // the list, so the window stays responsive with a loading indicator instead of freezing.
        _ = LoadEntriesAsync();
    }

    private async Task LoadEntriesAsync()
    {
        try
        {
            await HostsFile.PreloadAsync();
        }
        catch (Exception ex)
        {
            // LoadEntriesAsync is started fire-and-forget, so an exception from the off-thread load
            // (locked/denied hosts file, failed backup copy, bad HFE_HOSTS_PATH target) would be
            // unobserved. Set _loadFailed (NOT _isLoaded — the visibility getters would dereference
            // HostsFile.Instance, whose Lazy has cached this exception and would rethrow it out of
            // the binding refresh, killing the app before the dialog appears), stop the spinner,
            // show the error, and leave an inert empty list.
            _loadFailed = true;
            OnPropertyChanged(nameof(LoadingVisibility));
            OnPropertyChanged(nameof(EntriesEmptyVisibility));
            OnPropertyChanged(nameof(EntriesFilteredVisibility));
            await ShowErrorDialogAsync("Error Loading Hosts File", $"The hosts file could not be loaded:\n\n{ex.Message}");
            return;
        }

        // Back on the UI thread with the parse already done, so touching Instance is now cheap.
        HostsFile.Instance.Entries.ListChanged += OnCoreEntriesListChanged;
        _isLoaded = true;

        // Same bulk rebind + notifications as a filter change; the load also hides the spinner.
        RefreshEntriesFiltered();
        OnPropertyChanged(nameof(LoadingVisibility));
    }

    // One-shot bulk load of the (filtered) entries: build the list off the persistent collection
    // and rebind in a single operation, instead of hundreds of thousands of incremental adds.
    private void BulkPopulateEntries()
    {
        var filterText = CurrentFilterText();
        var filtered = HostsFile.Instance.Entries.Where(e => EntryPassesFilter(e, filterText)).ToList();
        Entries = new ObservableCollection<HostsEntry>(filtered);
        OnPropertyChanged(nameof(Entries));

        // A full rebind clears the ListView selection; keep the mirror and logical select-all flag
        // in sync (e.g. so a filter change after Ctrl+A doesn't leave "all" logically selected).
        ResetSelectionState();

        UpdateStatusCounts();
    }

    // Recomputes the status-bar counts from the whole hosts file (not the filtered view) — total
    // lines and enabled ("host") entries, matching the classic edition. One O(n) pass; called from
    // the (coalesced) refresh paths, so a burst of changes collapses to a single recount. Individual
    // checkbox toggles reach here via the Core ListChanged -> OnCoreEntriesListChanged -> RefreshEntries
    // path, so the enabled count stays live without hooking every row.
    private void UpdateStatusCounts()
    {
        string text;
        if (!_isLoaded)
        {
            text = string.Empty;
        }
        else
        {
            var entries = HostsFile.Instance.Entries;
            var total = entries.Count;
            var enabled = 0;
            for (var i = 0; i < total; i++)
            {
                if (entries[i].Enabled)
                {
                    enabled++;
                }
            }

            text = $"Line Count: {total:N0}      Host Entries: {enabled:N0}";
        }

        if (_statusText != text)
        {
            _statusText = text;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    // A filter change can swap the entire visible set, so rebuild it with a single bulk rebind
    // rather than the per-item minimal diff in RefreshEntries — that diff is O(n^2) with hundreds
    // of thousands of ObservableCollection mutations for a large hosts file, which locks up the UI.
    // Selection is reset, which is the expected behavior when the filter changes.
    private void RefreshEntriesFiltered()
    {
        // Inert until the initial load completes (and forever if it failed, or while an async
        // reload is rebuilding the Core list on a background thread) — BulkPopulateEntries
        // dereferences HostsFile.Instance and enumerates its Entries.
        if (!_isLoaded)
        {
            return;
        }

        BulkPopulateEntries();
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
        _selectionService.UpdateSelectionDependentButtons();
    }

    // Resolves the trimmed filter text once (a visual-tree FindName lookup). Hoist this before a
    // Where(...) so an O(n) filter pass over the entries doesn't re-run FindName + Trim per row —
    // on a 400K-entry file that was ~400K tree walks and allocations per rebuild/keystroke/ItemChanged.
    private string CurrentFilterText()
    {
        return Content is FrameworkElement root && root.FindName("FilterTextBox") is TextBox ftb && ftb.Text is string s
            ? s.Trim()
            : string.Empty;
    }

    private bool EntryPassesFilter(HostsEntry e, string filterText)
    {
        if (IsFilterCommentsHidden && e.HasCommentOnly)
        {
            return false;
        }

        if (IsFilterDisabledHidden && !e.Enabled && !e.HasCommentOnly)
        {
            return false;
        }

        return string.IsNullOrEmpty(filterText) || e.ToString().Contains(filterText, StringComparison.OrdinalIgnoreCase);
    }

    private void OnCoreEntriesListChanged(object? sender, ListChangedEventArgs e)
    {
        // Explicit handlers (delete/cut/…) refresh the view themselves in one bulk rebind; don't
        // stack a second per-item diff on top of their large structural change.
        if (_suspendCoreListSync)
        {
            return;
        }

        // Structural events that reach here unsuppressed are whole-list Resets from undo/redo
        // (explicit handlers suspend the subscription). Route them to the bulk rebind: the per-item
        // minimal diff is O(n^2) against a wholesale change — Ctrl+Z after Cut-All would re-insert
        // 400K rows one at a time and hang for minutes — and the bulk swap also re-realizes every
        // row container, which is what makes an undone enable/disable-all (a silent per-item toggle)
        // repaint its checkboxes. Only ItemChanged (a ping result, a cell edit) takes the cheap diff,
        // and it must NOT clear a pending logical Select-All.
        _pendingStructuralRefresh |= e.ListChangedType != ListChangedType.ItemChanged;

        // Coalesce: a burst of ListChanged events (auto-ping results streaming in on a large file)
        // must collapse to one queued refresh per dispatcher pass, not one O(n) rebuild per event.
        if (_coreRefreshPending)
        {
            return;
        }

        _coreRefreshPending = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _coreRefreshPending = false;
            var structural = _pendingStructuralRefresh;
            _pendingStructuralRefresh = false;

            if (structural)
            {
                RefreshEntriesFiltered();
            }
            else
            {
                RefreshEntries(preserveSelection: true, preserveLogicalSelectAll: true);
            }
        });
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

            // Warn about unsaved changes before the window closes (true exit; WinUI has no tray).
            appWindow.Closing += OnAppWindowClosing;
        }
    }

    // Set once the user has chosen to discard/save so the re-close isn't intercepted again.
    private bool _forceClose;

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Before the load completes there is nothing user-modified to lose — and touching
        // HostsFile.Instance here would block the UI thread on the in-progress background parse
        // (or rethrow a cached load failure). Let the close proceed.
        if (!_isLoaded || _forceClose || !HostsFile.Instance.IsModified)
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

    private void SelectAllEntries()
    {
        if (!_isLoaded)
        {
            return;
        }

        if (Entries.Count > LogicalSelectAllThreshold)
        {
            // Track "all selected" logically rather than populating the native ListView selection:
            // WinUI clears SelectedItems one row at a time, so tearing down a huge native selection
            // (on the following delete/cut/filter) is O(n^2) and froze the app for minutes.
            //
            // Any PRE-EXISTING native selection must be cleared first, or its rows stay visibly
            // highlighted (and SelectedItem stays stale, mis-anchoring a later paste) while the
            // selection state says nothing is natively selected — e.g. click a row, Ctrl+A, Esc
            // would leave a highlighted row with every selection-gated command disabled. Clearing
            // is cheap for the small selections users click together; a huge native range
            // (Shift+Click) pays the platform's own teardown cost once, here, instead of
            // desyncing.
            if (_selectedEntries.Count > 0 || EntriesList.SelectedItem is not null)
            {
                _suspendSelectionTracking = true;
                try
                {
                    EntriesList.SelectedItems.Clear();
                }
                finally
                {
                    _suspendSelectionTracking = false;
                }
            }

            _selectedEntries.Clear();
            SetLogicalSelectAll(true);
            _selectionService.UpdateSelectionDependentButtons();
            _selectionService.UpdateContextMenuItems();
        }
        else
        {
            SetLogicalSelectAll(false);
            EntriesList.SelectAll();
        }
    }

    // Alt+Up/Down (move) and Ctrl+Alt+Up/Down (insert) can't be plain KeyboardAccelerators: the
    // ListView consumes the arrow key for navigation before the accelerator fires (so move never
    // worked, and insert only worked at the first/last row — the one spot the ListView can't
    // navigate). Handle them in the tunneling PreviewKeyDown, which runs before the ListView's own
    // key handling, and mark them handled so the selection doesn't also move.
    private void OnEntriesPreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // Ctrl+A: intercept the ListView's native select-all in the tunneling phase (before the
        // ListView handles it) so a large list is selected logically instead of populating the
        // native selection, whose later teardown is O(n^2) and hangs the app. A KeyboardAccelerator
        // does NOT suppress the ListView's built-in Ctrl+A, but a handled PreviewKeyDown does.
        if (e.Key == VirtualKey.A && IsKeyDown(VirtualKey.Control) && !IsTextBoxFocused())
        {
            SelectAllEntries();
            e.Handled = true;
            return;
        }

        // Esc clears a logical Select-All (which has no native highlight to click away).
        if (e.Key == VirtualKey.Escape && _logicalSelectAll)
        {
            ResetSelectionState();
            _selectionService.UpdateSelectionDependentButtons();
            _selectionService.UpdateContextMenuItems();
            e.Handled = true;
            return;
        }

        if (e.Key is not (VirtualKey.Up or VirtualKey.Down) || IsTextBoxFocused())
        {
            return;
        }

        var alt = IsKeyDown(VirtualKey.Menu);
        if (!alt)
        {
            return;
        }

        var ctrl = IsKeyDown(VirtualKey.Control);
        if (e.Key == VirtualKey.Up)
        {
            if (ctrl) { OnInsertAboveClick(this, new RoutedEventArgs()); } else { OnMoveUpClick(this, new RoutedEventArgs()); }
        }
        else
        {
            if (ctrl) { OnInsertBelowClick(this, new RoutedEventArgs()); } else { OnMoveDownClick(this, new RoutedEventArgs()); }
        }

        e.Handled = true;
    }

    private static bool IsKeyDown(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        try
        {
            var path = Win32FileDialogs.OpenFileDialog(GetHwnd(), "All Files (*.*)|*.*");
            if (!string.IsNullOrWhiteSpace(path))
            {
                // Bulk rebind: an import replaces every entry, so the per-item RefreshEntries diff
                // would be O(n^2) against all-new references (an hour-class hang on a large file).
                MutateCoreAndRefresh(() => HostsFile.Instance.Import(path));
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Importing Hosts File", $"An error occurred while importing the hosts file:\n\n{ex.Message}");
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

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
        if (!_isLoaded)
        {
            return;
        }

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
        if (EntriesList.SelectedItem is HostsEntry current)
        {
            MutateKeepingSelection(() => HostsFile.Instance.Entries.InsertAfter(current));
        }
    }

    private void OnInsertAboveClick(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItem is HostsEntry current)
        {
            MutateKeepingSelection(() => HostsFile.Instance.Entries.InsertBefore(current));
        }
    }

    private void OnAddToTop(object sender, RoutedEventArgs e)
    {
        // A newly added entry is blank (comment-only) and matches no active filter, so it would
        // be added invisibly and be uneditable. Clear filters first so the user can see and edit it.
        ClearAllFilters();

        // Bulk rebind, NOT the minimal diff: clearing an active filter here swaps the whole visible
        // set (the diff against a small filtered view would be O(n^2) inserts on a large file — the
        // exact case RefreshEntriesFiltered exists to avoid). The helper's rebind also raises the
        // empty/filtered visibility notifications; the view rebuilds from the core list, so nothing
        // is inserted into the view directly (which would add the entry twice and ignore filters).
        MutateCoreAndRefresh(() =>
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
        });
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
        var (selected, minIndex, _) = ScanSelectedRange();
        if (selected.Count == 0 || minIndex <= 0)
        {
            return;
        }

        // Anchor on the entry immediately above the selection (in the visible/filtered view), not on
        // an entry within the selection itself: MoveBefore(x, x) is a no-op.
        MoveEntries(selected, () => HostsFile.Instance.Entries.MoveBefore(selected, Entries[minIndex - 1]));
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        var (selected, _, maxIndex) = ScanSelectedRange();
        if (selected.Count == 0 || maxIndex < 0 || maxIndex >= Entries.Count - 1)
        {
            return;
        }

        // Anchor on the entry immediately below the selection (in the visible/filtered view), not on
        // an entry within the selection itself: MoveAfter(x, x) is a no-op.
        MoveEntries(selected, () => HostsFile.Instance.Entries.MoveAfter(selected, Entries[maxIndex + 1]));
    }

    // A small block move keeps its selection (the pleasant UX); a huge one (a Shift+Click range —
    // Ctrl+A above the threshold is logical and disables Move) must go through the bulk rebind:
    // the minimal diff issues one ObservableCollection.Move per selected row (O(k*n) memmoves) and
    // the native-selection restore is the O(k^2) WinUI teardown. Dropping the selection on an
    // enormous move beats minutes of hang.
    private void MoveEntries(List<HostsEntry> selected, Action move)
    {
        if (selected.Count > LogicalSelectAllThreshold)
        {
            MutateCoreAndRefresh(move);
        }
        else
        {
            MutateKeepingSelection(move);
        }
    }

    // One O(n) scan yields the selected entries (visible order) and the first/last selected indices,
    // reading only the selection mirror. The old handlers read EntriesList.SelectedItems (O(n^2) for a
    // large native selection) and did selected.Min/Max(Entries.IndexOf) (another O(k*n)).
    private (List<HostsEntry> Selected, int MinIndex, int MaxIndex) ScanSelectedRange()
    {
        var selected = new List<HostsEntry>();
        var minIndex = -1;
        var maxIndex = -1;
        for (var i = 0; i < Entries.Count; i++)
        {
            if (_selectedEntries.Contains(Entries[i]))
            {
                if (minIndex < 0)
                {
                    minIndex = i;
                }

                maxIndex = i;
                selected.Add(Entries[i]);
            }
        }

        return (selected, minIndex, maxIndex);
    }

    // For small structural edits that should KEEP the current selection (move, insert-above/below,
    // add) — unlike MutateCoreAndRefresh, which rebinds in bulk and resets selection. RefreshEntries
    // restores the selection from the mirror via its minimal diff. Suspends the Core-list subscription
    // so the op's single Reset/ItemAdded doesn't also enqueue a second, redundant refresh. The refresh
    // lives in finally so a throw mid-mutation (whose suppressed Reset may already have fired) can't
    // leave the view desynced from what Save would write.
    private void MutateKeepingSelection(Action mutate)
    {
        _suspendCoreListSync = true;
        try
        {
            mutate();
        }
        finally
        {
            _suspendCoreListSync = false;
            RefreshEntries(true);
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedEntries();
        if (items.Count > 0)
        {
            RemoveFromCoreAndRefresh(items);
        }

        // Update UI buttons when selection changes cause deletion
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
    }

    // Materializes the current selection as an ordered list from our own selection mirror, never
    // touching ListView.SelectedItems (whose Count and enumeration are both O(n^2) for a large
    // selection — the actual cause of the multi-minute Select-All + Remove/Cut hang). Both branches
    // are O(n) with O(1) hash lookups and preserve visible order.
    private List<HostsEntry> GetSelectedEntries()
    {
        // Key the "all" fast path on the explicit logical flag only. A count-based short-circuit
        // (_selectedEntries.Count >= Entries.Count) would wrongly return every row if the mirror
        // ever drifted above the visible count; the Where already returns all rows when everything
        // is natively selected, so the flag is the sole trigger.
        return _logicalSelectAll
            ? [.. Entries]
            : [.. Entries.Where(_selectedEntries.Contains)];
    }

    // Single place to flip the logical-select-all flag so the banner notification can't be forgotten.
    private void SetLogicalSelectAll(bool value)
    {
        if (_logicalSelectAll == value)
        {
            return;
        }

        _logicalSelectAll = value;
        OnPropertyChanged(nameof(LogicalSelectAllVisibility));
        OnPropertyChanged(nameof(SelectAllBannerText));
    }

    // Clears the whole selection state (mirror + logical flag). Used where the selection genuinely
    // goes away — a full view rebind (filter/load) or a bulk delete/cut.
    private void ResetSelectionState()
    {
        _selectedEntries.Clear();
        SetLogicalSelectAll(false);
    }

    // Runs a bulk Core mutation and rebinds the visible set once. Suspends selection-delta tracking
    // (the ItemsSource swap deselects every old row — a huge teardown delta we don't want to process)
    // and the Core-list ListChanged subscription (the mutation raises one Reset, which would otherwise
    // also enqueue the O(n^2) per-item RefreshEntries diff), rebuilds via the O(n) bulk path, then
    // resets the selection state. The rebind and reset live in finally so a throw mid-mutation can't
    // leave the view desynced from what Save would write (the suppressed Reset may already have
    // fired), a stuck suspend flag, or a stale "all selected".
    private void MutateCoreAndRefresh(Action mutate)
    {
        _suspendSelectionTracking = true;
        _suspendCoreListSync = true;
        try
        {
            mutate();
        }
        finally
        {
            _suspendCoreListSync = false;
            _suspendSelectionTracking = false;
            RefreshEntriesFiltered();
            ResetSelectionState();
        }
    }

    // Like MutateCoreAndRefresh, but PRESERVES the selection: the selected entries survive these ops
    // (Check/Uncheck toggles Enabled in place; Duplicate keeps the originals), so re-select them after
    // the one-shot bulk rebind instead of dropping the selection. Same perf as the resetting variant —
    // a single O(n) rebind (which also re-realizes the row containers so checkbox visuals reflect the
    // silent SetEnabled) — plus an O(k) reselect bounded by the logical-select-all threshold.
    private void MutateCoreAndRefreshPreservingSelection(Action mutate)
    {
        // Snapshot BEFORE the mutation (the rebind clears the live mirror). A logical Select-All is
        // captured as a flag, not a 400K set.
        var wasLogicalSelectAll = _logicalSelectAll;
        HashSet<HostsEntry>? snapshot = wasLogicalSelectAll ? null : [.. _selectedEntries];

        _suspendSelectionTracking = true;
        _suspendCoreListSync = true;
        try
        {
            mutate();
        }
        finally
        {
            _suspendCoreListSync = false;

            if (_isLoaded)
            {
                // One-shot bulk rebind (swaps ItemsSource, clears native selection + mirror, updates
                // counts) then restore the selection against the rebuilt view.
                BulkPopulateEntries();
                RestoreSelectionAfterRebind(wasLogicalSelectAll, snapshot);
            }

            _suspendSelectionTracking = false;

            OnPropertyChanged(nameof(EntriesEmptyVisibility));
            OnPropertyChanged(nameof(EntriesFilteredVisibility));
            _selectionService.UpdateSelectionDependentButtons();
            _selectionService.UpdateContextMenuItems();
        }
    }

    // Re-applies a snapshotted selection to the freshly rebuilt view. Sets the native ListView
    // selection AND the mirror directly (selection tracking is suspended by the caller, so the
    // SelectionChanged events won't do it for us). Entries that a toggle just filtered out of the view
    // (e.g. Hide Disabled + Uncheck) are simply absent from the rebuilt list and drop out correctly.
    private void RestoreSelectionAfterRebind(bool wasLogicalSelectAll, HashSet<HostsEntry>? snapshot)
    {
        if (wasLogicalSelectAll)
        {
            if (Entries.Count > LogicalSelectAllThreshold)
            {
                SetLogicalSelectAll(true);
            }
            else
            {
                EntriesList.SelectAll();
                foreach (var entry in Entries)
                {
                    _selectedEntries.Add(entry);
                }
            }

            return;
        }

        if (snapshot is null || snapshot.Count == 0)
        {
            return;
        }

        var survivors = Entries.Where(snapshot.Contains).ToList();
        foreach (var entry in survivors)
        {
            _selectedEntries.Add(entry);
        }

        // Re-populate the native selection for a normal-sized selection; skip it for an enormous one
        // (a Shift+Click range beyond the threshold) — re-Adding is the O(k^2) WinUI populate we avoid
        // elsewhere. The mirror still holds it, so selection-gated commands keep working.
        if (survivors.Count <= LogicalSelectAllThreshold)
        {
            foreach (var entry in survivors)
            {
                EntriesList.SelectedItems.Add(entry);
            }
        }
    }

    // Async twin of MutateCoreAndRefresh for mutations that re-read a file from disk (reload /
    // archive load): the multi-second parse runs off the UI thread so the window stays responsive.
    // The window re-enters the "loading" state for the duration — the spinner returns, and every
    // Instance-touching command and view rebuild no-ops via its _isLoaded guard, because the Core
    // list is being cleared and re-filled on a background thread and enumerating it concurrently
    // (a filter keystroke, a paste) would throw or read a half-built list.
    private async Task MutateCoreAndRefreshAsync(Action mutate)
    {
        _isLoaded = false;
        OnPropertyChanged(nameof(LoadingVisibility));
        _suspendSelectionTracking = true;
        _suspendCoreListSync = true;
        try
        {
            await Task.Run(mutate);
        }
        finally
        {
            _suspendCoreListSync = false;
            _suspendSelectionTracking = false;
            _isLoaded = true;
            RefreshEntriesFiltered();
            ResetSelectionState();
            OnPropertyChanged(nameof(LoadingVisibility));
        }
    }

    private void RemoveFromCoreAndRefresh(List<HostsEntry> items) =>
        MutateCoreAndRefresh(() => HostsFile.Instance.Entries.Remove(items));

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        var rows = GetSelectedEntries();
        if (rows.Count > 0)
        {
            _clipboardEntries = [.. rows.Select(r => new HostsEntry(r))];
        }
    }

    private void OnCutClick(object sender, RoutedEventArgs e)
    {
        var rows = GetSelectedEntries();
        if (rows.Count > 0)
        {
            _clipboardEntries = [.. rows.Select(r => new HostsEntry(r))];
            RemoveFromCoreAndRefresh(rows);
        }

        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
    }

    private void OnPasteClick(object sender, RoutedEventArgs e)
    {
        if (_clipboardEntries != null)
        {
            // Anchor on the native SelectedItem when present; with no anchor (logical Select-All,
            // nothing selected, or the empty list after Cut-All) Core APPENDS — matching the classic
            // edition, and never silently inserting at the top of the file (which would change
            // resolution precedence). Rebind in bulk (not the per-item RefreshEntries diff, which is
            // O(n^2) for pasting a whole file).
            var current = EntriesList.SelectedItem as HostsEntry;
            var clipboard = _clipboardEntries;
            MutateCoreAndRefresh(() => HostsFile.Instance.Entries.Insert(current, clipboard));

            _clipboardEntries = null;
        }

        _selectionService.UpdateContextMenuItems();
    }

    private void OnDuplicateClick(object sender, RoutedEventArgs e)
    {
        var rows = GetSelectedEntries();
        if (rows.Count == 0 && EntriesList.SelectedItem is HostsEntry current)
        {
            rows = [current];
        }

        if (rows.Count == 0)
        {
            return;
        }

        // Duplicate copies each row after its original in one O(n) rebind (a per-row InsertAfter loop
        // would be O(n^2) at 400K). Preserve the selection: the originals remain, so keep them selected.
        MutateCoreAndRefreshPreservingSelection(() => HostsFile.Instance.Entries.Duplicate(rows));
    }

    private async void OnDisableHostsClick(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

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
        if (!_isLoaded)
        {
            return;
        }

        var confirmed = await ShowConfirmationAsync("Reload hosts file?", "You will lose any unsaved changes. Continue?");
        if (!confirmed)
        {
            return;
        }

        try
        {
            // Honor the "remove default text" setting on reload, matching initial load and
            // Import; passing nothing would always strip the default hosts header. The reload
            // re-reads and re-parses the whole file — run it off the UI thread behind the loading
            // indicator like the initial load, instead of freezing the window for the parse.
            await MutateCoreAndRefreshAsync(() => HostsFile.Instance.Refresh(HostsFile.RemoveDefaultText));
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Reloading Hosts File", $"An error occurred while reloading the hosts file:\n\n{ex.Message}");
        }
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        // The default hosts text is a small embedded resource — a synchronous bulk rebind is fine.
        MutateCoreAndRefresh(() => HostsFile.Instance.RestoreDefault());
    }

    private void OnOpenInTextEditorClick(object sender, RoutedEventArgs e) => Utilities.FileOpener.OpenTextFile(HostsFile.DefaultHostFilePath);

    private void OnFilterTextChanged(object sender, TextChangedEventArgs e) => RefreshEntriesFiltered();

    private void OnFilterCommentsClick(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleMenuFlyoutItem t)
        {
            // IsChecked == true => hide comments
            IsFilterCommentsHidden = t.IsChecked == true;
            RefreshEntriesFiltered();
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
            RefreshEntriesFiltered();
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
            RefreshEntriesFiltered();
            OnPropertyChanged(nameof(IsFilterCommentsHidden));
            OnPropertyChanged(nameof(IsFilterDisabledHidden));
            OnPropertyChanged(nameof(ActiveFilterCount));
            OnPropertyChanged(nameof(ActiveFiltersBadgeVisibility));
        }
    }

    private void OnCheckClick(object sender, RoutedEventArgs e)
    {
        var rows = GetSelectedEntries();
        if (rows.Count == 0 && EntriesList.SelectedItem is HostsEntry current)
        {
            rows = [current];
        }

        if (rows.Count == 0)
        {
            return;
        }

        // If all selected are enabled, uncheck; otherwise check.
        var allEnabled = rows.All(r => r.Enabled);

        // SetEnabled toggles without per-item PropertyChanged (O(n) instead of O(n^2)), so the ListView
        // won't see the change — rebind in bulk. Preserve the selection: the toggled rows are the same
        // entries and stay selected (unless Hide Disabled now filters an unchecked one out).
        MutateCoreAndRefreshPreservingSelection(() => HostsFile.Instance.Entries.SetEnabled(rows, isEnabled: !allEnabled));
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
        if (!_isLoaded)
        {
            return;
        }

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
        if (!_isLoaded)
        {
            return;
        }

        if (ArchiveList.SelectedItem is HostsArchive archive)
        {
            try
            {
                // An archive can be as large as the hosts file itself: parse it off the UI thread
                // and rebind in bulk (the per-item diff is O(n^2) against all-new references).
                await MutateCoreAndRefreshAsync(() => HostsFile.Instance.Import(archive.FilePath));
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

    // Minimal-diff refresh: updates Entries in place so a SMALL change (one insert, one move, an
    // ItemChanged from a ping result or cell edit) doesn't flicker the ListView or lose scroll
    // position. Steps (numbers match the inline comments; there is deliberately no step 4):
    // 1. Capture the current selection from the mirror (never ListView.SelectedItems — O(k^2)).
    // 2. Build the filtered target list (filter text hoisted to one read).
    // 3. Fast path: identical reference sequence -> skip all collection mutation AND leave the
    //    native selection untouched.
    // 5. Otherwise minimal per-index diff (Add / Move / Insert, then trim trailing extras). This is
    //    O(k*n) for k out-of-place rows — fine for a single insert/move, which is all that reaches
    //    it; wholesale changes (bulk ops, undo/redo Resets, filter changes) go through
    //    RefreshEntriesFiltered's one-shot rebind instead.
    // 6. Restore (or clear) the native selection when the sequence changed; huge restores are
    //    dropped rather than replayed (WinUI's SelectedItems is O(k^2)).
    // 7. Property notifications for empty/filtered visibility and the select-all banner.
    // 8. Update selection-dependent buttons & context menu.
    private void RefreshEntries(bool preserveSelection = false, bool preserveLogicalSelectAll = false)
    {
        // Inert until the initial load completes / while an async reload rebuilds the Core list on
        // a background thread — see RefreshEntriesFiltered.
        if (!_isLoaded)
        {
            return;
        }

        // 1. Capture selection if needed (from the mirror, not the O(n^2) SelectedItems collection)
        HashSet<HostsEntry>? selectedSnapshot = null;
        if (preserveSelection && _selectedEntries.Count > 0)
        {
            selectedSnapshot = [.. _selectedEntries];
        }

        // 2. Build target filtered list through the shared predicate (same rule as BulkPopulateEntries).
        var filterText = CurrentFilterText();
        var newList = HostsFile.Instance.Entries.Where(e => EntryPassesFilter(e, filterText)).ToList();

        // 3. Fast path: identical sequence -> only adjust selection/properties
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

        // 6. Restore / clear selection — only touch the native selection when the visible sequence
        // actually changed. Reading/writing ListView.SelectedItems is O(n^2) for a large selection,
        // so an ItemChanged refresh (a cell edit or an auto-ping result, where `same` is true) must
        // leave the existing selection untouched instead of diffing it. Restores beyond the logical
        // select-all threshold are dropped rather than replayed: re-Adding a huge native selection
        // (reachable via Shift+Click ranges, which the Ctrl+A interception can't gate) is the
        // documented O(k^2) WinUI teardown — losing the highlight beats a minutes-long hang.
        if (!same)
        {
            EntriesList.SelectedItems.Clear();
            if (preserveSelection
                && selectedSnapshot is not null
                && selectedSnapshot.Count <= LogicalSelectAllThreshold)
            {
                foreach (var item in Entries.Where(selectedSnapshot.Contains))
                {
                    EntriesList.SelectedItems.Add(item);
                }
            }
            else if (selectedSnapshot is not null)
            {
                // The native selection is gone and the mirror must agree.
                ResetSelectionState();
            }
        }

        // A logical Select-All is superseded by any refresh from an explicit user action; only a
        // background property update (ItemChanged) is allowed to preserve it.
        if (!preserveLogicalSelectAll)
        {
            SetLogicalSelectAll(false);
        }

        // 7. Property notifications (possible count / filter changes). Re-raise the banner text too,
        // so a preserved logical Select-All whose visible count changed doesn't show a stale number.
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
        if (_logicalSelectAll)
        {
            OnPropertyChanged(nameof(SelectAllBannerText));
        }

        UpdateStatusCounts();

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
        // Maintain the selection mirror from the per-change deltas (cheap — we only touch the items
        // that changed this event, never the whole SelectedItems collection). Skipped while a bulk
        // op rebinds the view, since that teardown churn is handled by resetting the mirror.
        if (!_suspendSelectionTracking)
        {
            // A real native selection change supersedes a prior logical select-all.
            if (e.AddedItems.Count > 0 || e.RemovedItems.Count > 0)
            {
                SetLogicalSelectAll(false);
            }

            foreach (HostsEntry removed in e.RemovedItems)
            {
                _selectedEntries.Remove(removed);
            }

            foreach (HostsEntry added in e.AddedItems)
            {
                _selectedEntries.Add(added);
            }
        }

        // Ctrl+A on a very large list fires SelectionChanged in a rapid burst (one per growing
        // selection step). The state refresh below only reads whether *any* row is selected, so
        // coalesce the burst into a single deferred update instead of running it 400K times.
        if (_selectionUpdatePending)
        {
            return;
        }

        _selectionUpdatePending = true;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            _selectionUpdatePending = false;
            _selectionService.UpdateSelectionDependentButtons();
            _selectionService.UpdateContextMenuItems();
        });
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
