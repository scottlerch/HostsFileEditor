using System.ComponentModel;
using HostsFileEditor.Utilities;

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
    public void MergeLines_AddsOnlyNewEntries_AndSkipsDuplicates()
    {
        var list = new HostsEntryList(["127.0.0.1 localhost", "1.2.3.4 existing.com"], filterDefault: false);

        var added = list.MergeLines(
        [
            "1.2.3.4 existing.com",     // duplicate (same IP + host) -> skipped
            "5.6.7.8 new.com",          // new -> added
            "9.9.9.9 also-new.com",     // new -> added
        ]);

        added.ShouldBe(2);
        list.Count.ShouldBe(4);
        list.Any(e => e.IpAddress == "5.6.7.8" && e.HostNames == "new.com").ShouldBeTrue();
    }

    [TestMethod]
    public void MergeLines_DuplicateIsCaseInsensitive_AndIgnoresEnabledState()
    {
        var list = new HostsEntryList(["1.2.3.4 Host.Example.COM"], filterDefault: false);

        // Same mapping, different case and commented out (disabled) — still a duplicate.
        var added = list.MergeLines(["# 1.2.3.4 host.example.com"]);

        added.ShouldBe(0);
        list.Count.ShouldBe(1);
    }

    [TestMethod]
    public void MergeLines_SkipsCommentsAndBlanksFromIncomingFile()
    {
        var list = new HostsEntryList(["127.0.0.1 localhost"], filterDefault: false);

        var added = list.MergeLines(["# a comment", "", "   ", "8.8.8.8 dns.example.com"]);

        added.ShouldBe(1);
        list.Count.ShouldBe(2);
    }

    [TestMethod]
    public void MergeLines_DedupesWithinTheIncomingSet()
    {
        var list = new HostsEntryList();

        var added = list.MergeLines(["1.2.3.4 dup.com", "1.2.3.4 dup.com", "1.2.3.4 dup.com"]);

        added.ShouldBe(1);
        list.Count.ShouldBe(1);
    }

    [TestMethod]
    public void MergeLines_DuplicateAcrossDifferentIpSpellings()
    {
        var list = new HostsEntryList(["::1 localhost"], filterDefault: false);

        // Same address written in the long form — canonical identity matches, so it's a duplicate.
        var added = list.MergeLines(["0:0:0:0:0:0:0:1 localhost"]);

        added.ShouldBe(0);
        list.Count.ShouldBe(1);
    }

    [TestMethod]
    public void MergeLines_IsUndoable()
    {
        var list = new HostsEntryList(["1.2.3.4 a.com"], filterDefault: false);
        UndoManager.Instance.ClearHistory();

        list.MergeLines(["5.6.7.8 b.com", "9.9.9.9 c.com"]).ShouldBe(2);
        list.Count.ShouldBe(3);

        // The append is one undoable step: Ctrl+Z removes the whole merged block.
        UndoManager.Instance.Undo();
        list.Count.ShouldBe(1);
        list.Single().IpAddress.ShouldBe("1.2.3.4");

        UndoManager.Instance.ClearHistory();
    }

    [TestMethod]
    public void MergeLines_NoOpMerge_RecordsNoUndo()
    {
        var list = new HostsEntryList(["1.2.3.4 a.com"], filterDefault: false);
        UndoManager.Instance.ClearHistory();

        // Every incoming line is already present — nothing is added, so nothing is recorded (the
        // document must not be marked modified for a no-op merge).
        list.MergeLines(["1.2.3.4 a.com", "# a comment", ""]).ShouldBe(0);

        UndoManager.Instance.CanUndo.ShouldBeFalse();

        UndoManager.Instance.ClearHistory();
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
    public void MoveBefore_RaisesSingleResetListChanged()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        list.Add(a); list.Add(b); list.Add(c);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        // A move must surface as one Reset — the old per-row base.Remove + Insert loop fired many
        // events and was O(k*n), the last O(n^2) hang in the app on a large multi-row move.
        list.MoveBefore([c], b);

        events.ShouldBe([ListChangedType.Reset]);
        list[1].ShouldBe(c);
        list[2].ShouldBe(b);
    }

    [TestMethod]
    public void MoveAfter_ThenUndoThenRedo_RoundTrips()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var entries = "a b c d e".Split(' ').Select(n => new HostsEntry($"127.0.0.1 {n}")).ToList();
        foreach (var entry in entries) { list.Add(entry); }

        list.MoveAfter([entries[1], entries[2]], entries[3]); // -> a d b c e

        list.Select(x => x.HostNames).ShouldBe(["a", "d", "b", "c", "e"]);

        UndoManager.Instance.Undo();
        list.Select(x => x.HostNames).ShouldBe(["a", "b", "c", "d", "e"]);

        UndoManager.Instance.Redo();
        list.Select(x => x.HostNames).ShouldBe(["a", "d", "b", "c", "e"]);
    }

    [TestMethod]
    public void Duplicate_ThenUndoThenRedo_PreservesCopyIdentity()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);

        list.Duplicate([a]);
        var copy = list[1]; // the copy created by Duplicate
        copy.ShouldNotBeSameAs(a);

        UndoManager.Instance.Undo();
        list.Count.ShouldBe(1);

        // Redo must restore the SAME copy instance (it rebuilds from the captured copy, not a fresh
        // new HostsEntry), so object identity is stable across undo/redo.
        UndoManager.Instance.Redo();
        list.Count.ShouldBe(2);
        list[1].ShouldBeSameAs(copy);
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
    public void SetEnabled_RaisesSingleResetListChanged()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        list.Add(a); list.Add(b);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        // A batch enable/disable must surface as exactly one Reset — NOT one ItemChanged per row.
        // Per-row events make the bound Equin BindingListView react O(n^2) (hung ~2 min at 400K).
        list.SetEnabled([a, b], isEnabled: false);

        events.ShouldBe([ListChangedType.Reset]);
        a.Enabled.ShouldBeFalse();
        b.Enabled.ShouldBeFalse();
    }

    [TestMethod]
    public void SetEnabled_AllAlreadyInState_NoOp()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a"); // enabled by default
        list.Add(a);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        list.SetEnabled([a], isEnabled: true); // already enabled

        events.ShouldBeEmpty();
    }

    [TestMethod]
    public void SetEnabled_UpdatesSerializedLine()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);

        list.SetEnabled([a], isEnabled: false);

        // The silent toggle must still invalidate the cached line so a save writes the disabled form.
        a.UnparsedText.ShouldStartWith("#");
    }

    [TestMethod]
    public void SetEnabled_ThenUndoThenRedo_RoundTrips()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("# comment"); // already disabled/comment; SetEnabled(true) shouldn't touch enable-state semantics
        list.Add(a); list.Add(b); list.Add(c);

        list.SetEnabled([a, b], isEnabled: false);
        a.Enabled.ShouldBeFalse();
        b.Enabled.ShouldBeFalse();

        UndoManager.Instance.Undo();
        a.Enabled.ShouldBeTrue();
        b.Enabled.ShouldBeTrue();

        UndoManager.Instance.Redo();
        a.Enabled.ShouldBeFalse();
        b.Enabled.ShouldBeFalse();
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

    [TestMethod]
    public void Remove_MultipleEntries_KeepsSurvivorsInOrder()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        var d = new HostsEntry("127.0.0.1 d");
        list.Add(a); list.Add(b); list.Add(c); list.Add(d);

        list.Remove([b, d]);

        list.Count.ShouldBe(2);
        list[0].ShouldBe(a);
        list[1].ShouldBe(c);
    }

    [TestMethod]
    public void Remove_RaisesSingleResetListChanged()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        list.Add(a); list.Add(b); list.Add(c);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        list.Remove([a, b]);

        // A bulk delete must surface as exactly one Reset — not one event per removed row — so
        // bound views rebind once instead of doing O(n) work per removed item.
        events.ShouldBe([ListChangedType.Reset]);
    }

    [TestMethod]
    public void Remove_EntriesNotInList_LeavesListUnchangedAndRaisesNoEvent()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        list.Add(a); list.Add(b);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        // Neither entry is in the list — must be a true no-op: no reset, no spurious undo entry.
        list.Remove([new HostsEntry("127.0.0.1 x"), new HostsEntry("127.0.0.1 y")]);

        events.ShouldBeEmpty();
        list.Count.ShouldBe(2);
        list[0].ShouldBe(a);
        list[1].ShouldBe(b);
    }

    [TestMethod]
    public void Remove_EmptyEnumerable_NoOp()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        list.Remove([]);

        events.ShouldBeEmpty();
        list.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Remove_ThenUndo_RestoresEntriesInOriginalOrder()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        list.Add(a); list.Add(b); list.Add(c);

        list.Remove([a, c]);
        list.Count.ShouldBe(1);

        UndoManager.Instance.Undo();

        list.Count.ShouldBe(3);
        list[0].ShouldBe(a);
        list[1].ShouldBe(b);
        list[2].ShouldBe(c);
    }

    [TestMethod]
    public void Remove_ThenUndoThenRedo_RemovesAgain()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        list.Add(a); list.Add(b); list.Add(c);

        list.Remove([a, c]);
        UndoManager.Instance.Undo();
        UndoManager.Instance.Redo();

        list.Count.ShouldBe(1);
        list[0].ShouldBe(b);
    }

    [TestMethod]
    public void Remove_UndoThenNewRemoveThenRedo_DoesNotResurrectStaleRedo()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        list.Add(a); list.Add(b); list.Add(c);

        list.Remove([a]);              // -> [b, c]
        UndoManager.Instance.Undo();   // -> [a, b, c]
        list.Remove([c]);              // -> [a, b]; must truncate the pending "remove a" redo branch

        UndoManager.Instance.Redo();   // nothing valid to redo — the stale "remove a" must not fire

        list.Count.ShouldBe(2);
        list[0].ShouldBe(a);
        list[1].ShouldBe(b);
    }

    [TestMethod]
    public void InsertAfter_RaisesSingleResetListChanged()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        list.Add(a); list.Add(b);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        // A single insert must surface as one Reset — a raw mid-list ItemAdded makes the bound
        // DataGridView shift/unshare every row (O(n^2); hung ~2 min on one insert at 400K).
        list.InsertAfter(a, new HostsEntry("127.0.0.1 c"));

        events.ShouldBe([ListChangedType.Reset]);
        list.Count.ShouldBe(3);
        list[1].HostNames.ShouldBe("c");
    }

    [TestMethod]
    public void InsertAfter_ThenUndoThenRedo_RoundTrips()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);

        list.InsertAfter(a, new HostsEntry("127.0.0.1 b"));
        list.Count.ShouldBe(2);

        UndoManager.Instance.Undo();
        list.Count.ShouldBe(1);
        list[0].ShouldBe(a);

        UndoManager.Instance.Redo();
        list.Count.ShouldBe(2);
        list[1].HostNames.ShouldBe("b");
    }

    [TestMethod]
    public void InsertRange_AppendsToEndAndRaisesSingleReset()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        list.InsertRange([new HostsEntry("127.0.0.1 b"), new HostsEntry("127.0.0.1 c")]);

        events.ShouldBe([ListChangedType.Reset]);
        list.Count.ShouldBe(3);
        list[1].HostNames.ShouldBe("b");
        list[2].HostNames.ShouldBe("c");
    }

    [TestMethod]
    public void InsertRange_IntoEmptyList_Populates()
    {
        // Models Paste after Cut-All: the list is empty (no anchor), so the clipboard is appended.
        var list = new HostsEntryList();

        list.InsertRange([new HostsEntry("127.0.0.1 a"), new HostsEntry("127.0.0.1 b")]);

        list.Count.ShouldBe(2);
        list[0].HostNames.ShouldBe("a");
        list[1].HostNames.ShouldBe("b");
    }

    [TestMethod]
    public void Duplicate_InsertsCopyAfterEachOriginal()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        var c = new HostsEntry("127.0.0.1 c");
        list.Add(a); list.Add(b); list.Add(c);

        list.Duplicate([a, c]);

        // a and c each get a copy immediately after them; b (not selected) is untouched.
        list.Count.ShouldBe(5);
        list[0].ShouldBe(a);
        list[1].ShouldNotBeSameAs(a);
        list[1].HostNames.ShouldBe("a");
        list[2].ShouldBe(b);
        list[3].ShouldBe(c);
        list[4].ShouldNotBeSameAs(c);
        list[4].HostNames.ShouldBe("c");
    }

    [TestMethod]
    public void Duplicate_DoesNotDuplicateTheCopies()
    {
        // Duplicating every row must yield exactly 2N, not more: the freshly inserted copies must
        // not themselves be treated as originals to duplicate.
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        list.Add(a); list.Add(b);

        list.Duplicate([a, b]);

        list.Count.ShouldBe(4);
        list[0].ShouldBe(a);
        list[1].ShouldNotBeSameAs(a);
        list[2].ShouldBe(b);
        list[3].ShouldNotBeSameAs(b);
    }

    [TestMethod]
    public void Duplicate_RaisesSingleResetListChanged()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        list.Add(a); list.Add(b);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        list.Duplicate([a, b]);

        events.ShouldBe([ListChangedType.Reset]);
    }

    [TestMethod]
    public void Duplicate_EmptyCollection_NoOp()
    {
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        list.Add(a);

        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        list.Duplicate([]);

        events.ShouldBeEmpty();
        list.Count.ShouldBe(1);
    }

    [TestMethod]
    public void Duplicate_ThenUndoThenRedo_RoundTrips()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var a = new HostsEntry("127.0.0.1 a");
        var b = new HostsEntry("127.0.0.1 b");
        list.Add(a); list.Add(b);

        list.Duplicate([a]);           // -> [a, a', b]
        list.Count.ShouldBe(3);

        UndoManager.Instance.Undo();   // -> [a, b]
        list.Count.ShouldBe(2);
        list[0].ShouldBe(a);
        list[1].ShouldBe(b);

        UndoManager.Instance.Redo();   // -> [a, a', b]
        list.Count.ShouldBe(3);
        list[1].HostNames.ShouldBe("a");
        list[2].ShouldBe(b);
    }

    [TestMethod]
    public void Insert_NullAnchor_AppendsToEnd()
    {
        var list = new HostsEntryList();
        list.Add(new HostsEntry("127.0.0.1 a"));

        // The paste contract both UIs share: no anchor row -> append (e.g. after Cut-All, or with
        // nothing selected). Core owns the fallback so the UIs can't drift.
        list.Insert(null, [new HostsEntry("127.0.0.1 b"), new HostsEntry("127.0.0.1 c")]);

        list.Count.ShouldBe(3);
        list[1].HostNames.ShouldBe("b");
        list[2].HostNames.ShouldBe("c");
    }

    [TestMethod]
    public void MoveBefore_AnchorInsideMovingSet_IsTrueNoOp()
    {
        UndoManager.Instance.ClearHistory();
        var list = new HostsEntryList();
        var entries = "a b c d".Split(' ').Select(n => new HostsEntry($"127.0.0.1 {n}")).ToList();
        foreach (var entry in entries)
        {
            list.Add(entry);
        }

        UndoManager.Instance.ClearHistory();
        var events = new List<ListChangedType>();
        list.ListChanged += (_, e) => events.Add(e.ListChangedType);

        // Anchor b sits INSIDE the moving set — the contract is a complete no-op: no reorder,
        // no ListChanged, no undo step, no IsModified change. (An anchor mid-set would otherwise
        // ambiguously reorder the remaining rows around it.)
        list.MoveBefore([entries[1], entries[2]], entries[1]);

        list.Select(x => x.HostNames).ShouldBe(["a", "b", "c", "d"]);
        events.ShouldBeEmpty();
        UndoManager.Instance.CanUndo.ShouldBeFalse();
    }

    [TestMethod]
    public void UndoAll_AfterHistoryEviction_TokenDoesNotReportClean()
    {
        UndoManager.Instance.ClearHistory();
        var cleanToken = UndoManager.Instance.CurrentStateToken;

        var list = new HostsEntryList();

        // One more op than MaximumHistorySize (1000), so the oldest group is evicted.
        for (var i = 0; i < 1001; i++)
        {
            list.Add(new HostsEntry($"127.0.0.1 h{i}"));
        }

        while (UndoManager.Instance.CanUndo)
        {
            UndoManager.Instance.Undo();
        }

        // The oldest Add(s) were evicted and can never be undone: their entries are still applied,
        // so the state is NOT back at the captured baseline. The token must not compare clean —
        // this is what keeps HostsFile.IsModified from skipping the unsaved-changes prompt after a
        // long editing session followed by undo-all.
        list.Count.ShouldBeGreaterThan(0);
        UndoManager.Instance.CurrentStateToken.ShouldNotBeSameAs(cleanToken);
    }
}
