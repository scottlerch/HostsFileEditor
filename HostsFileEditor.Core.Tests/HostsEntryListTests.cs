namespace HostsFileEditor.Core.Tests;

[TestClass]
public class HostsEntryListTests
{
    [TestMethod]
    public void AddLines_SkipDefault()
    {
        var lines = new[] { "127.0.0.1 localhost", "::1 localhost" };
        var list = new HostsEntryList(lines, filterDefault: false);
        list.Count.ShouldBe(lines.Length);
    }

    [TestMethod]
    public void Add_AddsBlankEntry()
    {
        var list = new HostsEntryList();
        list.Add();
        list.Count.ShouldBe(1);
    }

    [TestMethod]
    public void MoveBefore_MovesEntries()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        list.Add(a);
        list.Add(b);
        list.Add(c);
        list.MoveBefore([c], b); // should move c before b leaving a at index0
        list[0].HostNames.ShouldBe("a");
        list[1].HostNames.ShouldBe("c");
        list[2].HostNames.ShouldBe("b");
    }

    [TestMethod]
    public void MoveAfter_MovesEntries()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        list.Add(a);
        list.Add(b);
        list.Add(c);
        list.MoveAfter([a], b); // move a after b -> order should remain a,b,c since a already before b? Actually a removed then inserted after b => b,a,c
        list[0].HostNames.ShouldBe("b");
        list[1].HostNames.ShouldBe("a");
    }

    // Regression coverage for the "Move Up"/"Move Down" UI actions, which anchor the move on
    // the entry adjacent to (not inside) the selection. Anchoring on an entry that is itself
    // part of the moving set (e.g. MoveBefore(selected, selected[^1])) is always a no-op -
    // that was the actual bug (fixed in the WinForm/WinUI callers, not here).
    [TestMethod]
    public void MoveBefore_MultiSelectionAnchoredAboveSelection_ShiftsBlockUp()
    {
        var list = new HostsEntryList();
        var entries = "a b c d e".Split(' ').Select(n => new HostsEntry($"127.0.0.1 {n}")).ToList();
        foreach (var entry in entries)
        {
            list.Add(entry);
        }

        // Select b,c and move up: anchor is a (the entry above the selection), not b or c.
        list.MoveBefore([entries[1], entries[2]], entries[0]);

        list[0].HostNames.ShouldBe("b");
        list[1].HostNames.ShouldBe("c");
        list[2].HostNames.ShouldBe("a");
        list[3].HostNames.ShouldBe("d");
        list[4].HostNames.ShouldBe("e");
    }

    [TestMethod]
    public void MoveAfter_MultiSelectionAnchoredBelowSelection_ShiftsBlockDown()
    {
        var list = new HostsEntryList();
        var entries = "a b c d e".Split(' ').Select(n => new HostsEntry($"127.0.0.1 {n}")).ToList();
        foreach (var entry in entries)
        {
            list.Add(entry);
        }

        // Select b,c and move down: anchor is d (the entry below the selection), not b or c.
        list.MoveAfter([entries[1], entries[2]], entries[3]);

        list[0].HostNames.ShouldBe("a");
        list[1].HostNames.ShouldBe("d");
        list[2].HostNames.ShouldBe("b");
        list[3].HostNames.ShouldBe("c");
        list[4].HostNames.ShouldBe("e");
    }

    [TestMethod]
    public void MoveBefore_AnchoredOnEntryWithinSelection_IsNoOp()
    {
        var list = new HostsEntryList();
        var entries = "a b c".Split(' ').Select(n => new HostsEntry($"127.0.0.1 {n}")).ToList();
        foreach (var entry in entries)
        {
            list.Add(entry);
        }

        list.MoveBefore(entries, entries[^1]);

        list[0].HostNames.ShouldBe("a");
        list[1].HostNames.ShouldBe("b");
        list[2].HostNames.ShouldBe("c");
    }

    [TestMethod]
    public void InsertBefore_AnchorNotInList_AppendsWithoutThrowing()
    {
        var list = new HostsEntryList();
        list.Add(new HostsEntry("127.0.0.1 a"));
        var orphan = new HostsEntry("127.0.0.1 orphan"); // never added (mimics a pending new-row)

        Should.NotThrow(() => list.InsertBefore(orphan, new HostsEntry("127.0.0.1 b")));
        list.Count.ShouldBe(2);
    }

    [TestMethod]
    public void Insert_AnchorNotInList_AppendsWithoutThrowing()
    {
        var list = new HostsEntryList();
        list.Add(new HostsEntry("127.0.0.1 a"));
        var orphan = new HostsEntry("127.0.0.1 orphan");

        Should.NotThrow(() => list.Insert(orphan, [new HostsEntry("127.0.0.1 b"), new HostsEntry("127.0.0.1 c")]));
        list.Count.ShouldBe(3);
    }

    [TestMethod]
    public void SetEnabled_DisablesEntries()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);
        list.SetEnabled([a], false);
        a.Enabled.ShouldBeFalse();
    }

    [TestMethod]
    public void InsertBefore_AddsEntry()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);
        list.InsertBefore(a, new HostsEntry("127.0.0.1 b"));
        list[0].HostNames.ShouldBe("b");
    }

    [TestMethod]
    public void InsertAfter_AddsEntry()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);
        list.InsertAfter(a, new HostsEntry("127.0.0.1 b"));
        list[1].HostNames.ShouldBe("b");
    }

    [TestMethod]
    public void Remove_RemovesEntries()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        list.Add(a); list.Add(b);
        list.Remove([a]);
        list.ShouldNotContain(a);
    }
}
