namespace HostsFileEditor.Services;

public class SelectionStateService
{
    private readonly Func<bool> _hasSelection;

    // Move/insert need a concrete anchor row, which a logical Select-All (no native selection)
    // doesn't provide — so they're gated on this rather than _hasSelection.
    private readonly Func<bool> _hasAnchoredSelection;

    private readonly Action<bool> _setRemoveEnabled;
    private readonly Action<bool> _setDuplicateEnabled;
    private readonly Action<bool> _setMoveUpEnabled;
    private readonly Action<bool> _setMoveDownEnabled;
    private readonly Action<bool> _setToggleEnabled;

    private readonly Action<bool> _setCtxCopyVis;
    private readonly Action<bool> _setCtxCutVis;
    private readonly Action<bool> _setCtxPasteVis;
    private readonly Action<bool> _setCtxAddAboveVis;
    private readonly Action<bool> _setCtxAddBelowVis;
    private readonly Action<bool, bool> _setUndoRedoVis;

    public SelectionStateService(
        Func<bool> hasSelection,
        Func<bool> hasAnchoredSelection,
        Action<bool> setRemoveEnabled,
        Action<bool> setDuplicateEnabled,
        Action<bool> setMoveUpEnabled,
        Action<bool> setMoveDownEnabled,
        Action<bool> setToggleEnabled,
        Action<bool> setCtxCopyVis,
        Action<bool> setCtxCutVis,
        Action<bool> setCtxPasteVis,
        Action<bool> setCtxAddAboveVis,
        Action<bool> setCtxAddBelowVis,
        Action<bool, bool> setUndoRedoVis)
    {
        _hasSelection = hasSelection ?? throw new ArgumentNullException(nameof(hasSelection));
        // Required, not defaulted to hasSelection: falling back would silently enable Move/Insert under
        // a logical Select-All (no anchor row) — exactly the case this distinct predicate exists to gate.
        _hasAnchoredSelection = hasAnchoredSelection ?? throw new ArgumentNullException(nameof(hasAnchoredSelection));
        _setRemoveEnabled = setRemoveEnabled ?? (_ => { });
        _setDuplicateEnabled = setDuplicateEnabled ?? (_ => { });
        _setMoveUpEnabled = setMoveUpEnabled ?? (_ => { });
        _setMoveDownEnabled = setMoveDownEnabled ?? (_ => { });
        _setToggleEnabled = setToggleEnabled ?? (_ => { });
        _setCtxCopyVis = setCtxCopyVis ?? (_ => { });
        _setCtxCutVis = setCtxCutVis ?? (_ => { });
        _setCtxPasteVis = setCtxPasteVis ?? (_ => { });
        _setCtxAddAboveVis = setCtxAddAboveVis ?? (_ => { });
        _setCtxAddBelowVis = setCtxAddBelowVis ?? (_ => { });
        _setUndoRedoVis = setUndoRedoVis ?? ((_, __) => { });
    }

    public void UpdateSelectionDependentButtons()
    {
        var hasSelection = _hasSelection();
        var hasAnchored = _hasAnchoredSelection();
        _setRemoveEnabled(hasSelection);
        _setDuplicateEnabled(hasSelection);
        _setMoveUpEnabled(hasAnchored);
        _setMoveDownEnabled(hasAnchored);
        _setToggleEnabled(hasSelection);
    }

    public void UpdateContextMenuItems()
    {
        var hasSelection = _hasSelection();
        var hasAnchored = _hasAnchoredSelection();

        _setCtxCopyVis(hasSelection);
        _setCtxCutVis(hasSelection);
        _setCtxPasteVis(hasSelection);

        // Add Above/Below insert relative to a single anchor row — needs a native selection.
        _setCtxAddAboveVis(hasAnchored);
        _setCtxAddBelowVis(hasAnchored);

        var undoManager = Utilities.UndoManager.Instance;
        var canUndo = undoManager.CanUndo;
        var canRedo = undoManager.CanRedo;

        _setUndoRedoVis(canUndo, canRedo);
    }
}
