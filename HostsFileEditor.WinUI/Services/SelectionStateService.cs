namespace HostsFileEditor.Services;

public class SelectionStateService
{
    private readonly Func<bool> _hasSelection;

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
        _setRemoveEnabled(hasSelection);
        _setDuplicateEnabled(hasSelection);
        _setMoveUpEnabled(hasSelection);
        _setMoveDownEnabled(hasSelection);
        _setToggleEnabled(hasSelection);
    }

    public void UpdateContextMenuItems()
    {
        var hasSelection = _hasSelection();

        _setCtxCopyVis(hasSelection);
        _setCtxCutVis(hasSelection);
        _setCtxPasteVis(hasSelection);

        _setCtxAddAboveVis(hasSelection);
        _setCtxAddBelowVis(hasSelection);

        var undoManager = Utilities.UndoManager.Instance;
        var canUndo = undoManager.CanUndo;
        var canRedo = undoManager.CanRedo;

        _setUndoRedoVis(canUndo, canRedo);
    }
}
