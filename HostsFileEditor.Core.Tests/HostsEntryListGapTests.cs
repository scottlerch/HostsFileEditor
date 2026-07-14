using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.ComponentModel;

namespace HostsFileEditor.Core.Tests;

/// <summary>Targeted coverage for <see cref="HostsEntryList"/> paths not exercised elsewhere.</summary>
[TestClass]
public sealed class HostsEntryListGapTests
{
    [TestInitialize]
    public void Init() => UndoManager.Instance.ClearHistory();

    [TestCleanup]
    public void Cleanup()
    {
        // PingAll / MergeLines(auto-ping) start fire-and-forget pings that mutate the process-global
        // ping counter on a thread-pool continuation. Drain them before yielding so they can't leak
        // into (and flake) the ping-activity tests in other classes. Bounded so a stuck ping can't hang.
        var deadline = Environment.TickCount64 + 10_000;
        while (HostsEntry.IsPingInProgress && Environment.TickCount64 < deadline)
        {
            Thread.Sleep(50);
        }
    }

    [TestMethod]
    public void Error_ReportsInvalidEntries_WhenAnyEntryInvalid()
    {
        var list = new HostsEntryList(["127.0.0.1 localhost", "999.999.999.999 bogus"], filterDefault: false);
        list.Error.ShouldBe(Resources.InvalidHostEntries);
    }

    [TestMethod]
    public void Error_Empty_WhenAllValid()
    {
        var list = new HostsEntryList(["127.0.0.1 localhost", "10.0.0.1 host.test"], filterDefault: false);
        list.Error.ShouldBeEmpty();
    }

    [TestMethod]
    public void PingAll_DoesNotThrow()
    {
        var list = new HostsEntryList(["127.0.0.1 localhost", "# comment"], filterDefault: false);
        Should.NotThrow(list.PingAll);
    }

    [TestMethod]
    public void MergeLines_WithAutoPingOn_PingsAddedEntries()
    {
        var original = HostsEntry.AutoPingIPAddress;
        HostsEntry.AutoPingIPAddress = true;
        try
        {
            var list = new HostsEntryList(["127.0.0.1 localhost"], filterDefault: false);
            var added = list.MergeLines(["127.0.0.2 added.test"]);
            added.ShouldBe(1);
        }
        finally
        {
            HostsEntry.AutoPingIPAddress = original;
        }
    }

    [TestMethod]
    public void MoveBefore_AnchorNotInList_IsNoOp()
    {
        var list = new HostsEntryList(["127.0.0.1 a", "10.0.0.1 b"], filterDefault: false);
        var stranger = new HostsEntry("8.8.8.8 stranger");

        list.MoveBefore([list[0]], stranger);

        list[0].HostNames.ShouldBe("a"); // unchanged
        list[1].HostNames.ShouldBe("b");
    }

    [TestMethod]
    public void MoveBefore_AnchorInsideMovingSet_IsNoOp()
    {
        var list = new HostsEntryList(["127.0.0.1 a", "10.0.0.1 b"], filterDefault: false);

        list.MoveBefore([list[0], list[1]], list[0]);

        list[0].HostNames.ShouldBe("a");
        list[1].HostNames.ShouldBe("b");
    }

    [TestMethod]
    public void Duplicate_EmptyCollection_IsNoOp()
    {
        var list = new HostsEntryList(["127.0.0.1 a"], filterDefault: false);
        list.Duplicate([]);
        list.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Duplicate_EntriesNotInList_IsNoOp()
    {
        var list = new HostsEntryList(["127.0.0.1 a"], filterDefault: false);
        var stranger = new HostsEntry("8.8.8.8 stranger");

        list.Duplicate([stranger]);

        list.Count.ShouldBe(1);
    }

    [TestMethod]
    public void AddNew_UsesAddNewCore()
    {
        var list = new HostsEntryList(["127.0.0.1 a"], filterDefault: false);
        var added = ((IBindingList)list).AddNew();
        added.ShouldBeOfType<HostsEntry>();
    }

    [TestMethod]
    public void RemoveAt_RegistersUndo_AndRemoves()
    {
        var list = new HostsEntryList(["127.0.0.1 a", "10.0.0.1 b"], filterDefault: false);
        UndoManager.Instance.ClearHistory();

        list.RemoveAt(0);

        list.Count.ShouldBe(1);
        list[0].HostNames.ShouldBe("b");
        UndoManager.Instance.CanUndo.ShouldBeTrue();

        UndoManager.Instance.Undo();
        list.Count.ShouldBe(2);
    }

    [TestMethod]
    public void Add_Parameterless_AppendsEmptyEntry()
    {
        var list = new HostsEntryList(["127.0.0.1 a"], filterDefault: false);
        list.Add();
        list.Count.ShouldBe(2);
    }
}
