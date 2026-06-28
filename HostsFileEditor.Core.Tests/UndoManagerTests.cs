using HostsFileEditor.Utilities;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public sealed class UndoManagerTests
{
    private sealed class Holder { public int State; }

    [TestInitialize]
    public void Setup() => UndoManager.Instance.ClearHistory();

    [TestMethod]
    public void AddActions_Undo_RestoreState()
    {
        var h = new Holder { State = 0 };
        UndoManager.Instance.AddActions(() => h.State = 0, () => h.State = 1);
        h.State = 1;
        UndoManager.Instance.Undo();
        h.State.ShouldBe(0);
        UndoManager.Instance.Redo();
        h.State.ShouldBe(1);
    }

    [TestMethod]
    public void BatchActions_GroupsIntoSingleUndo()
    {
        var h = new Holder { State = 0 };
        UndoManager.Instance.BatchActions(() =>
        {
            UndoManager.Instance.AddActions(() => h.State = 0, () => h.State = 10);
            UndoManager.Instance.AddActions(() => h.State = 10, () => h.State = 110);
        });
        h.State = 10;
        h.State = 110;
        UndoManager.Instance.Undo(); // undo both: second sets 10, first sets 0
        h.State.ShouldBe(0);
        UndoManager.Instance.Redo(); // redo both: first sets 10, second sets 110
        h.State.ShouldBe(110);
    }

    [TestMethod]
    public void ClearHistory_ResetsUndoRedo()
    {
        var h = new Holder { State = 0 };
        UndoManager.Instance.AddActions(() => h.State = 0, () => h.State = 1);
        h.State = 1;
        UndoManager.Instance.Undo();
        h.State.ShouldBe(0);
        UndoManager.Instance.ClearHistory();
        UndoManager.Instance.Redo();
        h.State.ShouldBe(0);
    }

    [TestMethod]
    public void SuspendUndo_DoesNotRecordUndo()
    {
        var h = new Holder { State = 0 };
        UndoManager.Instance.SuspendUndo(() =>
        {
            UndoManager.Instance.AddActions(() => h.State = 0, () => h.State = 1);
        });
        h.State = 1;
        UndoManager.Instance.Undo();
        h.State.ShouldBe(1);
    }

    [TestMethod]
    public void SuspendRedo_DoesNotRecordRedo()
    {
        var h = new Holder { State = 0 };
        UndoManager.Instance.SuspendRedo(() =>
        {
            UndoManager.Instance.AddActions(() => h.State = 0, () => h.State = 1);
        });
        h.State = 1;
        UndoManager.Instance.Undo();
        h.State.ShouldBe(0);
    }

    [TestMethod]
    public void SuspendUndoRedo_NoRecordings()
    {
        var h = new Holder { State = 0 };
        UndoManager.Instance.SuspendUndoRedo(() =>
        {
            UndoManager.Instance.AddActions(() => h.State = 0, () => h.State = 1);
        });
        h.State = 1;
        UndoManager.Instance.Undo();
        h.State.ShouldBe(1);
        UndoManager.Instance.Redo();
        h.State.ShouldBe(1);
    }

    [TestMethod]
    public void EnforceCapacity_ManyActions_UndoRedoStaysConsistent()
    {
        var h = new Holder { State = 0 };

        // Exceed the internal capacity (1000 groups) to force eviction of the oldest
        // history. The position pointers must not be left dangling by the trimming.
        for (var i = 1; i <= 1100; i++)
        {
            var prev = h.State;
            var next = i;
            UndoManager.Instance.AddActions(() => h.State = prev, () => h.State = next);
            h.State = next;
        }

        Should.NotThrow(() =>
        {
            while (UndoManager.Instance.CanUndo)
            {
                UndoManager.Instance.Undo();
            }

            while (UndoManager.Instance.CanRedo)
            {
                UndoManager.Instance.Redo();
            }
        });

        // Redoing all retained history returns to the most recent value.
        h.State.ShouldBe(1100);
    }
}
