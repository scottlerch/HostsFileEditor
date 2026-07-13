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

    // The window's load lifecycle as ONE explicit state, replacing three co-varying booleans (was
    // _isLoaded / _loadFailed / _asyncOperationInProgress) whose invalid combinations — e.g. the
    // impossible "loaded AND reloading" — were representable and were the recurring source of the
    // missed-guard bugs this window kept growing (issue #76). Only the valid transitions exist:
    // InitialLoading -> Loaded (initial parse done) or -> LoadFailed (it threw); Loaded <-> Reloading
    // (an async file op). _isClosed is deliberately NOT folded in — it is an orthogonal, cross-thread
    // (volatile) terminal flag that can be set from ANY load state (a close mid-load / mid-reload),
    // and the _suspend* flags are reentrancy suppression, a different axis again.
    private enum LoadState
    {
        // The initial background parse is still running; touching HostsFile.Instance would force that
        // (potentially multi-second) parse onto the UI thread. Nothing is user-modified yet, so a
        // close proceeds immediately.
        InitialLoading,

        // The parse completed; the list is interactive and HostsFile.Instance is cheap to touch.
        Loaded,

        // An async file op (import / reload / archive-load) is rebuilding the Core list off-thread.
        // The list is non-interactive, Instance must not be touched, and a close is vetoed until it
        // finishes (its continuation must run; it would otherwise skip the unsaved-changes prompt).
        Reloading,

        // The initial load threw. The Lazy<HostsFile> has CACHED that exception, so every later
        // HostsFile.Instance touch rethrows it — Instance must never be dereferenced. Terminal.
        LoadFailed,
    }

    // Starts in InitialLoading so the eager x:Bind evaluation during InitializeComponent doesn't force
    // the parse onto the UI thread.
    private LoadState _loadState = LoadState.InitialLoading;

    // Derived flags preserving the exact meaning of the booleans they replace, so every existing guard
    // reads identically: IsLoaded == the old _isLoaded; AsyncOperationInProgress == the old
    // _asyncOperationInProgress (true only during a reload); LoadFailed == the old _loadFailed.
    private bool IsLoaded => _loadState == LoadState.Loaded;
    private bool AsyncOperationInProgress => _loadState == LoadState.Reloading;
    private bool LoadFailed => _loadState == LoadState.LoadFailed;

    // Set once the window has closed, so the fire-and-forget load / async-op continuations skip their
    // UI touches instead of hitting closed XAML (the classic edition guards the same way with IsDisposed).
    private volatile bool _isClosed;

    // The entries list is interactive only once the initial load has completed and no async file
    // operation is running. Bound to EntriesList.IsEnabled so its TwoWay row bindings (the Enabled
    // checkbox, the IP/host/comment TextBoxes) can't mutate HostsEntry objects the background thread
    // is concurrently discarding. With one explicit state this is simply "Loaded" — the old
    // IsLoaded && !AsyncOperationInProgress could represent the impossible loaded-and-reloading case.
    public bool IsEntriesInteractive => _loadState == LoadState.Loaded;

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
    // the bulk rebind, and _pendingCountsRefresh whether any event could have changed the status-bar
    // counts (structural, or an Enabled/unattributed ItemChanged) so ping storms skip the O(n)
    // recount. volatile: ListChanged can arrive off the UI thread (see _suspendCoreListSync).
    private volatile bool _coreRefreshPending;
    private volatile bool _pendingStructuralRefresh;
    private volatile bool _pendingCountsRefresh;

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
    // Shared with the classic edition via Core so the "huge selection" boundary stays in parity.
    private const int LogicalSelectAllThreshold = HostsEntryList.HugeSelectionThreshold;

    // True when Ctrl+A selected "all" logically (native selection left empty). GetSelectedEntries
    // resolves it to the full Entries list; any real selection change clears it. Mutate only via
    // SetLogicalSelectAll so the banner (LogicalSelectAllVisibility) stays in sync.
    private bool _logicalSelectAll;

    // Since a logical Select-All shows no row highlight, surface a banner so the user can see that
    // "all" is selected (otherwise a following Delete/Cut is a surprise).
    public Visibility LogicalSelectAllVisibility => _logicalSelectAll ? Visibility.Visible : Visibility.Collapsed;

    public string SelectAllBannerText => $"All {Entries.Count:N0} entries selected — press Esc or click a row to clear.";

    // Display-sort state (issue #81). Null column == file order. The sort is a VIEW ordering applied
    // to the filtered set in BulkPopulateEntries; it never mutates HostsFile.Instance.Entries, so Save
    // still writes the user's file order, and it is not an undoable edit. It re-applies automatically
    // across filter changes, reloads, and bulk edits because every rebind routes through
    // BulkPopulateEntries.
    private HostsEntry.SortColumn? _sortColumn;
    private bool _sortDescending;

    // True while a display sort is active. Position-relative commands (Move Up/Down, Insert
    // Above/Below) are disabled while sorted — "above/below" in a sorted view does not correspond to
    // the file order those commands reorder.
    private bool IsSortActive => _sortColumn is not null;

    // Visibility of the "sort active" badge overlaid on the Sort options button — the sort analog of
    // the filter's ActiveFiltersBadge, so a glance shows whether a sort is applied.
    public Visibility SortActiveBadgeVisibility => IsSortActive ? Visibility.Visible : Visibility.Collapsed;

    // Glyph shown in that badge: the icon of the currently sorted column (same glyphs as the menu
    // items), so the overlay tells you WHICH column is sorted, not just that a sort is on. Empty when
    // no sort is active (the badge is collapsed then anyway). Consumed only by UpdateSortBadgeIcon.
    private string SortActiveGlyph => _sortColumn switch
    {
        HostsEntry.SortColumn.IpAddress => "\uE774",
        HostsEntry.SortColumn.HostNames => "\uE8EC",
        HostsEntry.SortColumn.Comment => "\uE90A",
        HostsEntry.SortColumn.Enabled => "\uE739",
        HostsEntry.SortColumn.Valid => "\uE946",
        _ => string.Empty,
    };

    public Visibility LoadingVisibility => IsLoaded || LoadFailed ? Visibility.Collapsed : Visibility.Visible;

    // Bottom status bar text: total lines and host (enabled) entries, mirroring the classic edition's
    // status strip (HostsFile.LineCount / EnabledCount). Recomputed on each view refresh; see
    // UpdateStatusCounts.
    private string _statusText = string.Empty;

    public string StatusText => _statusText;

    // Ping-in-progress indicator (issue #9): visible while any ping is in flight, driven by
    // HostsEntry.PingActivityChanged (see OnPingActivityChanged).
    public Visibility PingProgressVisibility =>
        HostsEntry.IsPingInProgress ? Visibility.Visible : Visibility.Collapsed;

    private void OnPingActivityChanged(object? sender, EventArgs e)
    {
        // Marshalled to the UI thread by HostsEntry; a late notification can still arrive after close.
        if (_isClosed)
        {
            return;
        }

        OnPropertyChanged(nameof(PingProgressVisibility));
    }

    // Above this many visible rows (at 100% scale), show the opaque backplate behind the status
    // bar. WinUI stops clipping the bottom-edge rows of an enormous virtualized list (see the XAML
    // comment on the status-bar Grid for the full investigation) and the strays would show through
    // the transparent Mica band. Empirically clean at 10K rows and broken between 10K and 50K —
    // measured at 200% scale only, which cannot distinguish whether the underlying limit is
    // DIP- or physical-pixel-denominated. The escape is raster-level, which favors physical, so the
    // row count is scaled by the current RasterizationScale: at 100% the trigger is 10K (the
    // measured-safe margin), at 200% it is 5K, at 300% ~3.3K — conservative under either
    // hypothesis, while every realistic hosts file keeps the true-Mica look.
    private const int StatusBackplateThreshold = 10_000;

    public Visibility StatusBackplateVisibility =>
        Entries.Count * (Content?.XamlRoot?.RasterizationScale ?? 1.0) > StatusBackplateThreshold
            ? Visibility.Visible
            : Visibility.Collapsed;

    // The status row is 0 while the archive view is shown: its content collapses via
    // MainViewVisibility, but a FIXED 32px grid row keeps its height regardless — which left a
    // permanent dead band of blank Mica under the archive panel.
    public GridLength StatusRowHeight => IsArchiveVisible ? new GridLength(0) : new GridLength(32);

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

    // Both getters check LoadFailed BEFORE dereferencing HostsFile.Instance — see LoadFailed.
    public Visibility EntriesEmptyVisibility =>
        LoadFailed || (IsLoaded && HostsFile.Instance.Entries.Count == 0) ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EntriesFilteredVisibility =>
        !LoadFailed && IsLoaded && HostsFile.Instance.Entries.Count > 0 && Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

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
            // Move Up/Down and Insert Above/Below reorder the FILE relative to an anchor row, which is
            // meaningless while a display sort is applied (issue #81) — so gate them on !IsSortActive
            // too, disabling those commands (button + context menu) while sorted.
            hasAnchoredSelection: () => _selectedEntries.Count > 0 && !IsSortActive,
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

        // Ping-in-progress indicator (issue #9): PingActivityChanged is marshalled to this UI context,
        // so the handler runs on the UI thread and just re-evaluates PingProgressVisibility.
        HostsEntry.PingActivityChanged += OnPingActivityChanged;

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
            _isClosed = true;
            Utilities.UndoManager.Instance.HistoryChanged -= OnUndoHistoryChanged;
            HostsEntry.PingActivityChanged -= OnPingActivityChanged;

            // Unsubscribe unconditionally: the subscription is added once (after the initial load) and
            // persists across reloads, but a reload enters Reloading (IsLoaded false) — gating on it
            // would leak the subscription (and this window, via HostsFile.Instance) if the user closes
            // mid-reload. `-=` on a not-yet-subscribed handler is a harmless no-op.
            HostsFile.Instance.Entries.ListChanged -= OnCoreEntriesListChanged;
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
            // unobserved. Set LoadFailed (NOT IsLoaded — the visibility getters would dereference
            // HostsFile.Instance, whose Lazy has cached this exception and would rethrow it out of
            // the binding refresh, killing the app before the dialog appears), stop the spinner,
            // show the error, and leave an inert empty list.
            _loadState = LoadState.LoadFailed;
            OnPropertyChanged(nameof(LoadingVisibility));
            OnPropertyChanged(nameof(EntriesEmptyVisibility));
            OnPropertyChanged(nameof(EntriesFilteredVisibility));
            await ShowErrorDialogAsync("Error Loading Hosts File", $"The hosts file could not be loaded:\n\n{ex.Message}");
            return;
        }

        // The window may have been closed during the multi-second background parse; the continuation
        // would otherwise touch disposed XAML (the classic edition guards the same way).
        if (_isClosed)
        {
            return;
        }

        // Back on the UI thread with the parse already done, so touching Instance is now cheap.
        HostsFile.Instance.Entries.ListChanged += OnCoreEntriesListChanged;
        _loadState = LoadState.Loaded;

        // Same bulk rebind + notifications as a filter change; the load also hides the spinner.
        RefreshEntriesFiltered();
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(IsEntriesInteractive));

        // Taskbar Jump List (issue #10): publish the current presets and keep it in sync as archives
        // change. Fire-and-forget — RefreshAsync no-ops when unpackaged (JumpList needs identity).
        _ = TaskbarJumpList.RefreshAsync();
        HostsArchiveList.Instance.ListChanged += (s, e) => _ = TaskbarJumpList.RefreshAsync();

        // If a Jump List activation arrived before the load finished, honor it now.
        if (_pendingJumpListArchive is { } pending)
        {
            _pendingJumpListArchive = null;
            await ImportArchiveFromJumpListAsync(pending);
        }
    }

    // Set by RequestOpenArchive when a Jump List activation arrives before the initial load completes;
    // LoadEntriesAsync imports it once loaded.
    private string? _pendingJumpListArchive;

    /// <summary>
    /// Opens the preset a taskbar Jump List entry pointed at (issue #10) — called by App on both a
    /// fresh launch and a redirect to this already-running instance. If the initial load is still in
    /// flight, the request is deferred until it completes. Must be called on the UI thread.
    /// </summary>
    public void RequestOpenArchive(string archivePath)
    {
        if (_isClosed || string.IsNullOrEmpty(archivePath))
        {
            return;
        }

        if (IsLoaded)
        {
            _ = ImportArchiveFromJumpListAsync(archivePath);
        }
        else
        {
            // Loading (or reloading) — remember it; LoadEntriesAsync will pick it up.
            _pendingJumpListArchive = archivePath;
        }
    }

    private async Task ImportArchiveFromJumpListAsync(string archivePath)
    {
        // Fire-and-forget from the activation path — catch everything so a failure is logged, not
        // silently swallowed (or crashing the app).
        try
        {
            if (_isClosed || !File.Exists(archivePath))
            {
                return;
            }

            // Opening a preset replaces the current entries, so warn before discarding unsaved edits.
            if (HostsFile.Instance.IsModified)
            {
                var confirmed = await ShowConfirmationAsync(
                    "Open preset?",
                    "You have unsaved changes to the hosts file that will be lost. Open the preset anyway?",
                    primaryText: "Open preset",
                    closeText: "Cancel");

                if (!confirmed || _isClosed)
                {
                    return;
                }
            }

            await MutateCoreAndRefreshAsync(() => HostsFile.Instance.Import(archivePath));
        }
        catch (Exception ex)
        {
            if (!_isClosed)
            {
                await ShowErrorDialogAsync("Error Opening Preset", $"An error occurred while opening the preset:\n\n{ex.Message}");
            }
        }
    }

    // One-shot bulk load of the (filtered) entries: build the list off the persistent collection
    // and rebind in a single operation, instead of hundreds of thousands of incremental adds.
    private void BulkPopulateEntries(List<HostsEntry>? prefiltered = null)
    {
        // Filter-change callers pass null and we compute the filtered set here (hoisting the filter
        // text to one lookup — NOT per entry); the RefreshEntries huge-selection reroute already built
        // the identical set (same EntryPassesFilter predicate), so it passes it in to avoid filtering
        // the whole (up to ~400K) list a second time (#74).
        List<HostsEntry> filtered;
        if (prefiltered is not null)
        {
            filtered = prefiltered;
        }
        else
        {
            var filterText = CurrentFilterText();
            filtered = [.. HostsFile.Instance.Entries.Where(e => EntryPassesFilter(e, filterText))];
        }

        // Apply the display sort (issue #81) to the filtered COPY — never to HostsFile.Instance.Entries
        // — so Save writes file order. A stable order (OrderBy) keeps ties in file order, which avoids
        // the "everything scrambles" surprise when sorting a boolean column (Enabled/Status).
        if (_sortColumn is { } sortColumn)
        {
            filtered = [.. filtered.OrderBy(e => e, HostsEntry.GetComparer(sortColumn, _sortDescending))];
        }

        Entries = new ObservableCollection<HostsEntry>(filtered);
        OnPropertyChanged(nameof(Entries));

        // A full rebind clears the ListView selection; keep the mirror and logical select-all flag
        // in sync (e.g. so a filter change after Ctrl+A doesn't leave "all" logically selected).
        ResetSelectionState();

        UpdateStatusCounts();
    }

    // Recomputes the status-bar counts from the whole hosts file (not the filtered view) via the
    // SAME Core properties the classic edition's status strip binds (HostsFile.LineCount /
    // EnabledCount) — one definition of the counts across editions. O(n) via EnabledCount; called
    // from the (coalesced) refresh paths, so a burst of changes collapses to a single recount, and
    // pure ping-result passes skip it entirely (see _pendingCountsRefresh). Individual checkbox
    // toggles reach here via the Core ListChanged -> OnCoreEntriesListChanged -> RefreshEntries
    // path, so the enabled count stays live without hooking every row.
    private void UpdateStatusCounts()
    {
        string text;
        if (!IsLoaded)
        {
            text = string.Empty;
        }
        else
        {
            var hostsFile = HostsFile.Instance;
            text = $"Line Count: {hostsFile.LineCount:N0}      Host Entries: {hostsFile.EnabledCount:N0}";
        }

        if (_statusText != text)
        {
            _statusText = text;
            OnPropertyChanged(nameof(StatusText));
        }

        // Cheap: x:Bind just re-reads Entries.Count and the rasterization scale. Raised on every
        // recount so the backplate tracks the visible row count through loads, filters, and bulk edits.
        OnPropertyChanged(nameof(StatusBackplateVisibility));
    }

    // A filter change can swap the entire visible set, so rebuild it with a single bulk rebind
    // rather than the per-item minimal diff in RefreshEntries — that diff is O(n^2) with hundreds
    // of thousands of ObservableCollection mutations for a large hosts file, which locks up the UI.
    // Selection is reset, which is the expected behavior when the filter changes.
    private void RefreshEntriesFiltered(List<HostsEntry>? prefiltered = null)
    {
        // Inert until the initial load completes (and forever if it failed, or while an async
        // reload is rebuilding the Core list on a background thread) — BulkPopulateEntries
        // dereferences HostsFile.Instance and enumerates its Entries.
        if (!IsLoaded)
        {
            return;
        }

        BulkPopulateEntries(prefiltered);
        OnPropertyChanged(nameof(EntriesEmptyVisibility));
        OnPropertyChanged(nameof(EntriesFilteredVisibility));
        _selectionService.UpdateSelectionDependentButtons();
    }

    // Applies (or clears) the display sort (issue #81) and rebinds, PRESERVING the selection by
    // identity across the reorder — the sort "mutation" is just setting the sort fields; the existing
    // selection-preserving rebind does the O(n) re-sort (in BulkPopulateEntries) and O(k) reselect.
    private void ApplySort(HostsEntry.SortColumn column, bool descending)
    {
        if (!IsLoaded)
        {
            return;
        }

        MutateCoreAndRefreshPreservingSelection(() =>
        {
            _sortColumn = column;
            _sortDescending = descending;
        });

        OnSortStateChanged();
    }

    private void ClearSort()
    {
        if (!IsLoaded || !IsSortActive)
        {
            return;
        }

        MutateCoreAndRefreshPreservingSelection(() => _sortColumn = null);
        OnSortStateChanged();
    }

    // Everything that must refresh after _sortColumn changes: Move/Insert enablement depends on
    // IsSortActive (button + context menu), and the sort badge reflects the new column.
    private void OnSortStateChanged()
    {
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
        OnPropertyChanged(nameof(SortActiveBadgeVisibility));
        UpdateSortBadgeIcon();
    }

    // The Segoe MDL2 symbol font never varies; share one instance across badge updates.
    private static readonly FontFamily SortBadgeFontFamily = new("Segoe MDL2 Assets");

    // Assigns a FRESH FontIconSource to the sort badge for the current column. InfoBadge does not
    // re-render when the Glyph changes inside the same IconSource object, so swapping the whole object
    // is what makes the overlay track the selected column (issue #81).
    private void UpdateSortBadgeIcon() =>
        SortActiveBadge.IconSource = _sortColumn is null
            ? null
            : new FontIconSource
            {
                FontFamily = SortBadgeFontFamily,
                FontSize = 10,
                Glyph = SortActiveGlyph,
            };

    // "Sort options" menu (issue #81): a RadioMenuFlyoutItem whose Tag is a HostsEntry.SortColumn name.
    // The direction toggle is OnSortDirectionClick; clearing back to file order is OnResetSortClick.
    private void OnSortColumnClick(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: string tag } && Enum.TryParse<HostsEntry.SortColumn>(tag, out var column))
        {
            ApplySort(column, _sortDescending);
        }
    }

    private void OnSortDirectionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleMenuFlyoutItem toggle)
        {
            return;
        }

        _sortDescending = toggle.IsChecked;

        // Re-apply only when a column is active; direction alone means nothing in file order.
        if (_sortColumn is { } column)
        {
            ApplySort(column, _sortDescending);
        }
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

    // Delegates to the single canonical predicate in Core (issue #75) so classic and modern filter
    // identically. filterText is pre-trimmed once by CurrentFilterText and passed in, keeping the
    // per-row cost out of the 400K filter pass.
    private bool EntryPassesFilter(HostsEntry e, string filterText) =>
        HostsEntry.MatchesFilter(e, IsFilterCommentsHidden, IsFilterDisabledHidden, filterText);

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

        // The status counts only change when the list changes shape or an Enabled flag flips. A
        // ping result (an IpAddress ItemChanged) cannot change them, and recounting the whole file
        // per coalesced pass added an O(n) tax to every wave of a 400K ping storm. A null
        // descriptor (possible under trimming, where BindingList's TypeDescriptor lookup may not
        // resolve) recounts — never a stale count, just today's cost.
        _pendingCountsRefresh |= e.ListChangedType != ListChangedType.ItemChanged
            || e.PropertyDescriptor is null
            || e.PropertyDescriptor.Name == nameof(HostsEntry.Enabled);

        // Coalesce: a burst of ListChanged events (auto-ping results streaming in on a large file)
        // must collapse to one queued refresh per dispatcher pass, not one O(n) rebuild per event.
        if (_coreRefreshPending)
        {
            return;
        }

        _coreRefreshPending = true;
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _coreRefreshPending = false;

            // A ListChanged (e.g. a streaming ping result) can enqueue this after the window has
            // closed; the awaited load/async continuations guard on _isClosed, and so must this
            // fire-and-forget callback, or RefreshEntries touches torn-down XAML (RO_E_CLOSED).
            if (_isClosed)
            {
                return;
            }

            var structural = _pendingStructuralRefresh;
            _pendingStructuralRefresh = false;
            var updateCounts = _pendingCountsRefresh;
            _pendingCountsRefresh = false;

            if (structural)
            {
                RefreshEntriesFiltered();
            }
            else
            {
                RefreshEntries(preserveSelection: true, preserveLogicalSelectAll: true, updateCounts);
            }
        }))
        {
            // Enqueue failed (dispatcher shutting down): reset the coalescing flag so a later
            // ListChanged can re-enqueue instead of being suppressed forever by a stuck pending flag.
            _coreRefreshPending = false;
        }
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

            // Enforce the minimum window size at the window level. A root-Grid MinHeight can't do
            // this: when the window is shorter than it, the Grid overflows and the OS clips the
            // bottom row (the status bar) off-screen instead of shrinking the star content row — so
            // the status bar vanished when the window was shortened even with room to spare.
            //
            // PreferredMinimum* are PHYSICAL pixels fed to WM_GETMINMAXINFO with no DPI awareness
            // (microsoft-ui-xaml #10452/#10475), while the chrome they protect (42epx title row,
            // command bar, 32epx status row) is laid out in DIPs — a raw 300x320 stopped protecting
            // the status row at >=250% display scale. Scale the DIP floor at apply time and
            // re-apply whenever the scale changes (XamlRoot.Changed fires on DPI/monitor moves);
            // XamlRoot is still null in the constructor, so the scaled value lands on Loaded.
            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                void ApplyMinimumWindowSize()
                {
                    var scale = Content?.XamlRoot?.RasterizationScale ?? 1.0;
                    presenter.PreferredMinimumWidth = (int)Math.Ceiling(300 * scale);
                    presenter.PreferredMinimumHeight = (int)Math.Ceiling(320 * scale);
                }

                ApplyMinimumWindowSize();
                if (Content is FrameworkElement rootElement)
                {
                    var xamlRootChangedHooked = false;
                    rootElement.Loaded += (_, _) =>
                    {
                        ApplyMinimumWindowSize();

                        // Loaded can fire more than once (content re-parenting / some theme or DPI
                        // transitions); subscribe XamlRoot.Changed only once, or a handler accretes on
                        // every Loaded and each DPI change then runs the work N times for the app's life.
                        if (xamlRootChangedHooked || rootElement.XamlRoot is null)
                        {
                            return;
                        }

                        xamlRootChangedHooked = true;
                        rootElement.XamlRoot.Changed += (_, _) =>
                        {
                            ApplyMinimumWindowSize();

                            // The backplate trigger is scale-dependent too (raster-level escape —
                            // see StatusBackplateVisibility).
                            OnPropertyChanged(nameof(StatusBackplateVisibility));
                        };
                    };
                }
            }

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
        // An async file operation (import/reload/archive-load) is mid-flight rebuilding the Core
        // list on a background thread. Veto the close: letting it proceed would both skip the
        // unsaved-changes prompt (below, gated on IsLoaded which is false during the op) and run
        // the operation's continuation against a closed window. The user can close once it finishes.
        if (AsyncOperationInProgress)
        {
            args.Cancel = true;
            return;
        }

        // Before the load completes there is nothing user-modified to lose — and touching
        // HostsFile.Instance here would block the UI thread on the in-progress background parse
        // (or rethrow a cached load failure). Let the close proceed.
        if (!IsLoaded || _forceClose || !HostsFile.Instance.IsModified)
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
        if (!IsLoaded)
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

        // Esc clears a logical Select-All (which has no native highlight to click away). Skip it while
        // a cell TextBox is focused (as the Ctrl+A branch does): a logical Select-All can still be
        // active during an in-cell edit, and swallowing Esc here would eat the TextBox's own revert.
        if (e.Key == VirtualKey.Escape && _logicalSelectAll && !IsTextBoxFocused())
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
        if (!IsLoaded)
        {
            return;
        }

        try
        {
            var path = Win32FileDialogs.OpenFileDialog(GetHwnd(), "All Files (*.*)|*.*");
            if (!string.IsNullOrWhiteSpace(path))
            {
                // Off the UI thread behind the spinner, exactly like reload and archive-load (an
                // import reads an arbitrary user-chosen file that can be as large as the hosts file
                // itself — running it inline froze the window for the whole multi-second parse).
                // The async helper also rebinds in bulk: an import replaces every entry, so the
                // per-item RefreshEntries diff would be O(n^2) against all-new references.
                await MutateCoreAndRefreshAsync(() => HostsFile.Instance.Import(path));
            }
        }
        catch (Exception ex)
        {
            await ShowErrorDialogAsync("Error Importing Hosts File", $"An error occurred while importing the hosts file:\n\n{ex.Message}");
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
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
        if (!IsLoaded)
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
        // IsSortActive: "below" in a sorted view has no defined file position (issue #81). Button is
        // disabled while sorted; this also blocks the Ctrl+Down path.
        if (!IsLoaded || IsSortActive)
        {
            return;
        }

        if (EntriesList.SelectedItem is HostsEntry current)
        {
            MutateKeepingSelection(() => HostsFile.Instance.Entries.InsertAfter(current));
        }
    }

    private void OnInsertAboveClick(object sender, RoutedEventArgs e)
    {
        // See OnInsertBelowClick: no position-relative insert while a display sort is active (#81).
        if (!IsLoaded || IsSortActive)
        {
            return;
        }

        if (EntriesList.SelectedItem is HostsEntry current)
        {
            MutateKeepingSelection(() => HostsFile.Instance.Entries.InsertBefore(current));
        }
    }

    private void OnAddToTop(object sender, RoutedEventArgs e)
    {
        // Inert while loading or after a FAILED load: the Lazy<HostsFile> has cached the load
        // exception, so this handler's Instance touch would rethrow it out of a click handler and
        // crash the app (there is no UnhandledException net) — and during an async reload it would
        // mutate the list the background thread is rebuilding.
        if (!IsLoaded)
        {
            return;
        }

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
        // IsSortActive: Move reorders the file relative to a displayed neighbor, which is meaningless
        // while sorted (issue #81). The button is already disabled; this also blocks the Alt+Up path.
        if (!IsLoaded || IsSortActive)
        {
            return;
        }

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
        // See OnMoveUpClick: no file moves while a display sort is active (issue #81).
        if (!IsLoaded || IsSortActive)
        {
            return;
        }

        var (selected, _, maxIndex) = ScanSelectedRange();
        if (selected.Count == 0 || maxIndex < 0 || maxIndex >= Entries.Count - 1)
        {
            return;
        }

        // Anchor on the entry immediately below the selection (in the visible/filtered view), not on
        // an entry within the selection itself: MoveAfter(x, x) is a no-op.
        MoveEntries(selected, () => HostsFile.Instance.Entries.MoveAfter(selected, Entries[maxIndex + 1]));
    }

    // A SMALL block move keeps its selection via the minimal diff (pleasant UX, viewport intact).
    // Anything larger routes through the selection-preserving bulk rebind: the minimal diff issues
    // one ObservableCollection.Move per selected row — each an O(n) memmove, so the cost is O(k·n),
    // which scales with the FILE size independent of any selection-teardown threshold (at 400K rows
    // even a 2K block is ~1s and 15K is tens of seconds of UI freeze) — plus O(k^2)-class native
    // selection churn. The k·n product guard protects mid-size files too. The bulk path restores
    // the moved block's selection (dropped above the logical-select-all threshold, like everywhere
    // else) and scrolls it back into view.
    private void MoveEntries(List<HostsEntry> selected, Action move)
    {
        if (selected.Count > 2_000 || (long)selected.Count * Entries.Count > 100_000_000L)
        {
            MutateCoreAndRefreshPreservingSelection(move, keepSelected: selected);
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
        if (!IsLoaded)
        {
            return;
        }

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
            // Tracking stays suspended THROUGH the rebind: the ItemsSource swap inside
            // RefreshEntriesFiltered fires the huge teardown SelectionChanged delta this flag
            // exists to skip (clearing it first made that comment a lie and paid the churn).
            RefreshEntriesFiltered();
            ResetSelectionState();
            _suspendSelectionTracking = false;
        }
    }

    // Like MutateCoreAndRefresh, but PRESERVES the selection: the selected entries survive these ops
    // (Check/Uncheck toggles Enabled in place; Duplicate keeps the originals), so re-select them after
    // the one-shot bulk rebind instead of dropping the selection. Same perf as the resetting variant —
    // a single O(n) rebind (which also re-realizes the row containers so checkbox visuals reflect the
    // silent SetEnabled) — plus an O(k) reselect bounded by the logical-select-all threshold.
    //
    // keepSelected: the explicit set to re-select afterwards. Duplicate MUST pass its pre-mutation
    // rows: a logical Select-All re-flagged over the doubled list would silently include the fresh
    // copies ("all selected" then deletes originals AND copies — emptying the file), while the
    // snapshot path correctly re-selects only the originals. Null = preserve whatever is selected
    // now (correct for SetEnabled, which adds no rows).
    private void MutateCoreAndRefreshPreservingSelection(Action mutate, IReadOnlyCollection<HostsEntry>? keepSelected = null)
    {
        // Snapshot BEFORE the mutation (the rebind clears the live mirror). A logical Select-All is
        // captured as a flag, not a 400K set — unless the caller pinned an explicit keep-set. A
        // keep-set larger than the threshold is dropped anyway (RestoreSelectionAfterRebind won't
        // repopulate a huge native selection), so skip building the snapshot for it.
        var wasLogicalSelectAll = _logicalSelectAll && keepSelected is null;
        HashSet<HostsEntry>? snapshot = keepSelected is { Count: > LogicalSelectAllThreshold }
            ? null
            : keepSelected is not null
                ? [.. keepSelected]
                : wasLogicalSelectAll ? null : [.. _selectedEntries];

        _suspendSelectionTracking = true;
        _suspendCoreListSync = true;
        try
        {
            mutate();
        }
        finally
        {
            _suspendCoreListSync = false;

            // One CANONICAL bulk rebind (gates on IsLoaded, swaps ItemsSource, resets selection
            // state, raises the empty/filtered notifications, refreshes counts and buttons) —
            // calling it instead of an inline copy keeps "what a rebind must notify" in one place.
            RefreshEntriesFiltered();

            if (IsLoaded)
            {
                var scrollTarget = RestoreSelectionAfterRebind(wasLogicalSelectAll, snapshot);

                // The ItemsSource swap resets the viewport to the top and SelectedItems.Add does
                // not scroll; bring the restored selection back into view.
                if (scrollTarget is not null)
                {
                    EntriesList.ScrollIntoView(scrollTarget);
                }
            }

            _suspendSelectionTracking = false;

            // Re-run the selection-dependent UI now that the restore repopulated the mirror (the
            // rebind's own update ran against the cleared state).
            _selectionService.UpdateSelectionDependentButtons();
            _selectionService.UpdateContextMenuItems();
        }
    }

    // Re-applies a snapshotted selection to the freshly rebuilt view and returns the row to scroll
    // back into view (null when nothing was restored). Sets the native ListView selection AND the
    // mirror directly (selection tracking is suspended by the caller, so the SelectionChanged events
    // won't do it for us). Entries that a toggle just filtered out of the view (e.g. Hide Disabled +
    // Uncheck) are simply absent from the rebuilt list and drop out correctly.
    private HostsEntry? RestoreSelectionAfterRebind(bool wasLogicalSelectAll, HashSet<HostsEntry>? snapshot)
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

            // Everything is selected — there is no meaningful scroll anchor.
            return null;
        }

        if (snapshot is null || snapshot.Count == 0)
        {
            return null;
        }

        var survivors = Entries.Where(snapshot.Contains).ToList();

        // Above the threshold restore NOTHING — not even the mirror. A mirror-only selection is a
        // PHANTOM: no row highlight, no banner (the flag is false, so Esc can't clear it either),
        // and the next click ADDS to it (SelectionChanged's RemovedItems is empty because nothing
        // was natively selected) — a follow-up Delete would then silently hit 20K+ invisible rows.
        // Dropping a huge selection matches the policy everywhere else (RefreshEntries, MoveEntries).
        if (survivors.Count > LogicalSelectAllThreshold)
        {
            return null;
        }

        foreach (var entry in survivors)
        {
            _selectedEntries.Add(entry);
            EntriesList.SelectedItems.Add(entry);
        }

        return survivors.Count > 0 ? survivors[0] : null;
    }

    // Async twin of MutateCoreAndRefresh for mutations that re-read a file from disk (import /
    // reload / archive load): the multi-second parse runs off the UI thread so the window stays
    // responsive. The window re-enters the "loading" state for the duration — the spinner returns,
    // and every Instance-touching command no-ops via its `if (!IsLoaded) return;` guard, because
    // the Core list is being cleared and re-filled on a background thread and mutating or
    // enumerating it concurrently (a delete, a paste, an undo) would corrupt the list or throw.
    private async Task MutateCoreAndRefreshAsync(Action mutate)
    {
        _loadState = LoadState.Reloading;
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(IsEntriesInteractive));

        // Drop the (now-stale) selection state up front so selection-gated buttons visibly
        // disable for the duration instead of merely no-op'ing through their guards.
        ResetSelectionState();
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();

        _suspendSelectionTracking = true;
        _suspendCoreListSync = true;
        try
        {
            await Task.Run(mutate);
        }
        finally
        {
            _suspendCoreListSync = false;
            _loadState = LoadState.Loaded;

            // The window may have closed during the parse (a close is vetoed while the op runs — see
            // OnAppWindowClosing — but be defensive): don't rebind against disposed XAML.
            if (!_isClosed)
            {
                // Tracking stays suspended through the rebind's ItemsSource swap — see MutateCoreAndRefresh.
                RefreshEntriesFiltered();
                ResetSelectionState();
                OnPropertyChanged(nameof(LoadingVisibility));
                OnPropertyChanged(nameof(IsEntriesInteractive));
            }

            _suspendSelectionTracking = false;
        }
    }

    private void RemoveFromCoreAndRefresh(List<HostsEntry> items) =>
        MutateCoreAndRefresh(() => HostsFile.Instance.Entries.Remove(items));

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        var rows = GetSelectedEntries();
        if (rows.Count > 0)
        {
            _clipboardEntries = [.. rows.Select(r => new HostsEntry(r))];
        }
    }

    private void OnCutClick(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

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
        if (!IsLoaded)
        {
            return;
        }

        if (_clipboardEntries != null)
        {
            // One anchoring contract regardless of HOW the selection was made: paste inserts BEFORE
            // the first selected row, and with no selection at all Core APPENDS. Previously the
            // anchor was just the native SelectedItem, so the same Ctrl+A → Ctrl+V landed at the TOP
            // of the file below the logical-select-all threshold (native select-all leaves the first
            // row as SelectedItem) but at the BOTTOM above it (logical select-all leaves it null) —
            // opposite file ends across an invisible 20K boundary. Rebind in bulk (not the per-item
            // RefreshEntries diff, which is O(n^2) for pasting a whole file).
            var current = EntriesList.SelectedItem as HostsEntry;
            if (current is null && _logicalSelectAll)
            {
                current = Entries.FirstOrDefault();
            }

            if (current is null && _selectedEntries.Count > 0)
            {
                var (_, minIndex, _) = ScanSelectedRange();
                if (minIndex >= 0)
                {
                    current = Entries[minIndex];
                }
            }

            var clipboard = _clipboardEntries;
            MutateCoreAndRefresh(() => HostsFile.Instance.Entries.Insert(current, clipboard));

            _clipboardEntries = null;
        }

        _selectionService.UpdateContextMenuItems();
    }

    private void OnDuplicateClick(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

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
        // would be O(n^2) at 400K). Preserve the ORIGINALS' selection by pinning them as the keep-set:
        // without it, a logical Select-All would re-flag "all" over the doubled list and a follow-up
        // Delete would remove originals AND copies (emptying the file).
        MutateCoreAndRefreshPreservingSelection(() => HostsFile.Instance.Entries.Duplicate(rows), keepSelected: rows);
    }

    private async void OnDisableHostsClick(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
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
        if (!IsLoaded)
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
        if (!IsLoaded)
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

    // Symmetry with Reset Filters: clear the display sort back to file order and reset the menu's
    // column radios + the Descending toggle to their default (unchecked / ascending). Programmatic
    // IsChecked changes don't raise Click, so this doesn't re-enter the sort handlers.
    private void OnResetSortClick(object sender, RoutedEventArgs e)
    {
        foreach (var item in SortMenuFlyout.Items)
        {
            if (item is RadioMenuFlyoutItem radio)
            {
                radio.IsChecked = false;
            }
        }

        _sortDescending = false;
        if (SortDescendingToggle is not null)
        {
            SortDescendingToggle.IsChecked = false;
        }

        ClearSort();
    }

    private void OnCheckClick(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

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

    // Undo/redo replay closures that mutate HostsFile.Instance.Entries, so they carry the same
    // IsLoaded guard as every other Instance-mutating command: during an async reload the
    // pre-clear undo history is briefly still live (Refresh reads the file before clearing it),
    // and replaying it on the UI thread would race the background rebuild of the same list.
    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            Utilities.UndoManager.Instance.Undo();
        }
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            Utilities.UndoManager.Instance.Redo();
        }
    }

    private void OnUndoAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnUndoClick(this, new RoutedEventArgs()), args);

    private void OnRedoAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        => TryInvokeUnlessTextBox(() => OnRedoClick(this, new RoutedEventArgs()), args);

    private async void OnArchiveClick(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
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
        if (!IsLoaded)
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
        // The archive panel stays enabled during an async archive load/import (IsEntriesInteractive
        // disables only the entries list), so without this guard a Delete could race File.Delete
        // against the still-open read handle of the archive being imported (IOException -> crash).
        if (!IsLoaded)
        {
            return;
        }

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
    // position. Steps (numbers match the inline comments):
    // 1. Capture the current selection from the mirror (never ListView.SelectedItems — O(k^2)).
    // 2. Build the filtered target list (filter text hoisted to one read).
    // 3. Fast path: identical reference sequence -> skip all collection mutation AND leave the
    //    native selection untouched.
    // 4. Changed sequence + huge selection -> route to the one-shot bulk rebind (the step-6 Clear
    //    would be the O(k^2) teardown, and the snapshot would be dropped anyway).
    // 5. Otherwise minimal per-index diff (RemoveAt vanished rows, Add / Move / Insert, then trim
    //    trailing extras). O(k*n) for k out-of-place rows — fine for the small changes that reach
    //    it; wholesale changes (bulk ops, undo/redo Resets, filter changes) go through
    //    RefreshEntriesFiltered's one-shot rebind instead.
    // 6. Restore (or clear) the native selection when the sequence changed; huge restores are
    //    dropped rather than replayed (WinUI's SelectedItems is O(k^2)).
    // 7. Property notifications for empty/filtered visibility and the select-all banner.
    // 8. Update selection-dependent buttons & context menu.
    private void RefreshEntries(bool preserveSelection = false, bool preserveLogicalSelectAll = false, bool updateCounts = true)
    {
        // Inert until the initial load completes / while an async reload rebuilds the Core list on
        // a background thread — see RefreshEntriesFiltered.
        if (!IsLoaded)
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

        // 4. A changed sequence under a HUGE selection routes to the one-shot bulk rebind instead
        // of the diff below: the unconditional SelectedItems.Clear() in step 6 is the documented
        // O(k^2) WinUI teardown for a big native range (Shift+Click ranges bypass the Ctrl+A
        // interception), and the snapshot would be dropped above the threshold anyway — so the diff
        // path would pay a minutes-class hang and STILL lose the selection. The rebind pays the
        // platform's teardown exactly once.
        if (!same && _selectedEntries.Count > LogicalSelectAllThreshold)
        {
            // Keep tracking suspended through the ItemsSource swap so its huge teardown
            // SelectionChanged delta is skipped (the reset clears the mirror), like the Mutate helpers.
            var wasSuspended = _suspendSelectionTracking;
            _suspendSelectionTracking = true;
            try
            {
                // Reuse the list we just filtered instead of letting BulkPopulateEntries filter the
                // whole ~400K list a second time (#74).
                RefreshEntriesFiltered(newList);
            }
            finally
            {
                _suspendSelectionTracking = wasSuspended;
            }

            return;
        }

        if (!same)
        {
            // 5. Minimal diff updates. Membership set for O(1) "is this row still wanted": without
            // it, a REMOVED row was Move-bubbled to the end one adjacent swap at a time — one
            // O(n) memmove per following row, O((n-p)^2) total, a minutes-class hang at 400K from a
            // single checkbox click under an active Hide-Disabled filter (even with nothing selected).
            // Built lazily: an append or an in-place move consults it zero or one times, so the
            // ~O(n) set (up to ~8MB at 400K) is allocated only when a row actually needs removing.
            HashSet<HostsEntry>? targetSet = null;
            for (var i = 0; i < newList.Count; i++)
            {
                var desired = newList[i];

                // Drop rows that are no longer in the target BEFORE searching for the desired one.
                while (i < Entries.Count
                    && !ReferenceEquals(Entries[i], desired)
                    && !(targetSet ??= [.. newList]).Contains(Entries[i]))
                {
                    Entries.RemoveAt(i);
                }

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

        // Counts recompute only when something could have changed them: the coalesced ItemChanged
        // path passes updateCounts=false for pure ping-result bursts (see OnCoreEntriesListChanged),
        // and a changed sequence always recounts.
        if (updateCounts || !same)
        {
            UpdateStatusCounts();
        }

        // 8. Update dependent UI
        _selectionService.UpdateSelectionDependentButtons();
        _selectionService.UpdateContextMenuItems();
    }

    private void RefreshArchives()
    {
        Archives.Clear();

        // Sorted by the shared Core comparer (ordinal-ignore-case file name), matching the classic
        // edition's archive view — instead of raw HostsArchiveList order, which is file-system
        // enumeration order at startup and append order for new archives.
        foreach (var a in HostsArchiveList.Instance.Order(HostsArchive.FileNameComparer))
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
        ApplyToggleSetting(
            "AutoPingIPs",
            v =>
            {
                IsPingIPs = v;
                HostsEntry.AutoPingIPAddress = v;

                // Ping the current entries immediately on enable (issue #9 follow-up) instead of
                // waiting for the next reload/edit, so results and the indicator appear right away.
                if (v && IsLoaded)
                {
                    HostsFile.Instance.Entries.PingAll();
                }
            },
            nameof(IsPingIPs),
            sender);

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
        if (!DispatcherQueue.TryEnqueue(() =>
        {
            _selectionUpdatePending = false;

            // The callback can drain after the window closed (or a teardown SelectionChanged queued it);
            // UpdateSelectionDependentButtons/UpdateContextMenuItems set IsEnabled/Visibility on XAML
            // peers, and `?.` guards null but not torn-down elements (RO_E_CLOSED). Guard _isClosed like
            // the OnCoreEntriesListChanged callback does.
            if (_isClosed)
            {
                return;
            }

            _selectionService.UpdateSelectionDependentButtons();
            _selectionService.UpdateContextMenuItems();
        }))
        {
            // Enqueue can fail while the dispatcher is shutting down (window closing). Don't leave the
            // flag stuck true, or every later SelectionChanged would early-return and stop refreshing
            // the selection-dependent UI.
            _selectionUpdatePending = false;
        }
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
