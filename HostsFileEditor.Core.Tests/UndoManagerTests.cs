using HostsFileEditor.Utilities;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class UndoManagerTests
{
    private class Holder { public int State; }

    [TestInitialize]
    public void Setup()
    {
        UndoManager.Instance.ClearHistory();
    }

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
}
