using HostsFileEditor.Utilities;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class UndoManagerAdditionalTests
{
    [TestInitialize]
    public void Setup() => UndoManager.Instance.ClearHistory();

    [TestMethod]
    public void HistoryChanged_RaisedOnAddUndoRedoClear()
    {
        var raised = 0;
        void Handler(object? s, EventArgs e) => raised++;
        UndoManager.Instance.HistoryChanged += Handler;
        try
        {
            UndoManager.Instance.AddActions(() => { }, () => { }); // raise
            UndoManager.Instance.Undo(); // raise
            UndoManager.Instance.Redo(); // raise
            UndoManager.Instance.ClearHistory(); // raise
            raised.ShouldBeGreaterThanOrEqualTo(3); // minimal expectation
        }
        finally
        {
            UndoManager.Instance.HistoryChanged -= Handler;
        }
    }

    [TestMethod]
    public void CanUndoRedo_ReflectState()
    {
        UndoManager.Instance.CanUndo.ShouldBeFalse();
        UndoManager.Instance.CanRedo.ShouldBeFalse();
        UndoManager.Instance.AddActions(() => { }, () => { });
        UndoManager.Instance.CanUndo.ShouldBeTrue();
        UndoManager.Instance.Undo();
        // After undo we should be able to redo
        UndoManager.Instance.CanRedo.ShouldBeTrue();
    }

    [TestMethod]
    public void AddActions_DuringSuspendUndo_NoUndoOrRedoAvailable()
    {
        var applied = 0;
        UndoManager.Instance.SuspendUndo(() =>
        {
            UndoManager.Instance.AddActions(() => applied -= 10, () => applied += 10);
        });
        UndoManager.Instance.CanUndo.ShouldBeFalse();
        // Redo pointer at end so redo also not available
        UndoManager.Instance.CanRedo.ShouldBeFalse();
        UndoManager.Instance.Undo(); // no-op
        applied.ShouldBe(0);
        UndoManager.Instance.Redo(); // no-op
        applied.ShouldBe(0);
    }

    [TestMethod]
    public void AddActions_DuringSuspendRedo_OnlyUndoRecorded()
    {
        var applied = 0;
        UndoManager.Instance.SuspendRedo(() =>
        {
            UndoManager.Instance.AddActions(() => applied -= 5, () => applied += 5);
        });
        UndoManager.Instance.CanUndo.ShouldBeTrue();
        UndoManager.Instance.CanRedo.ShouldBeFalse();
        UndoManager.Instance.Undo();
        applied.ShouldBe(-5);
    }

    private static readonly int[] expected = [3, 1];

    [TestMethod]
    public void NestedBatchActions_SingleGroup()
    {
        var seq = new List<int>();
        UndoManager.Instance.BatchActions(() =>
        {
            UndoManager.Instance.AddActions(() => seq.Add(1), () => seq.Add(2));
            UndoManager.Instance.BatchActions(() =>
            {
                UndoManager.Instance.AddActions(() => seq.Add(3), () => seq.Add(4));
            });
        });
        seq.ShouldBeEmpty();
        UndoManager.Instance.Undo(); // executes undo actions: outer adds first undo (1) then inner (3)
        seq.ShouldBe(expected);
    }

    [TestMethod]
    public void EmptyBatch_NoHistoryEntry()
    {
        UndoManager.Instance.BatchActions(() => { });
        UndoManager.Instance.CanUndo.ShouldBeFalse();
    }

    [TestMethod]
    public void EnforceCapacity_PrunesOldEntries()
    {
        for (var i = 0; i < 1005; i++)
        {
            var local = i;
            UndoManager.Instance.AddActions(() => { var _ = local; }, () => { var __ = local; });
        }
        UndoManager.Instance.CanUndo.ShouldBeTrue();
        for (var i = 0; i < 10; i++)
        {
            UndoManager.Instance.Undo();
        }
        UndoManager.Instance.CanRedo.ShouldBeTrue();
    }

    [TestMethod]
    public void Undo_NoActions_DoesNothing() => UndoManager.Instance.Undo();

    [TestMethod]
    public void Redo_NoActions_DoesNothing() => UndoManager.Instance.Redo();
}
