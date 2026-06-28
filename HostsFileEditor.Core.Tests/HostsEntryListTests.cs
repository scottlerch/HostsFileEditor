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
