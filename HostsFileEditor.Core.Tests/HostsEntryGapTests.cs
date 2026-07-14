using HostsFileEditor.Properties;
using HostsFileEditor.Utilities;
using System.Reflection;

namespace HostsFileEditor.Core.Tests;

/// <summary>Targeted coverage for <see cref="HostsEntry"/> branches not reached by the existing suite.</summary>
[TestClass]
public sealed class HostsEntryGapTests
{
    [TestInitialize]
    public void Init()
    {
        UndoManager.Instance.ClearHistory();
        HostsEntry.AutoPingIPAddress = false;
        HostsEntry.UiSynchronizationContext = null;
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Stop new auto-pings, then wait for any fire-and-forget ping this test started to finish
        // before yielding to the next test. HostsEntry.Ping() is fire-and-forget and mutates the
        // process-global ping counter (BeginPing/EndPing) plus raises the static PingActivityChanged
        // on a thread-pool continuation; leaving one in flight would flake the ping-activity tests
        // here and the pre-existing PingActivity_RaisesOnZeroBoundaryOnly.
        HostsEntry.AutoPingIPAddress = false;
        SpinUntil(() => !HostsEntry.IsPingInProgress, TimeSpan.FromSeconds(10));
        HostsEntry.UiSynchronizationContext = null;
    }

    // ---- GetComparer: the remaining columns and the descending string wrapper ----

    [TestMethod]
    public void GetComparer_HostNames_Descending()
    {
        var list = new List<HostsEntry> { new("1.1.1.1 a"), new("2.2.2.2 c"), new("3.3.3.3 b") };
        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.HostNames, descending: true));
        list.Select(e => e.HostNames).ShouldBe(["c", "b", "a"]);
    }

    [TestMethod]
    public void GetComparer_Comment_BothDirections()
    {
        var list = new List<HostsEntry>
        {
            new("1.1.1.1 h # bbb"),
            new("2.2.2.2 h # aaa"),
            new("3.3.3.3 h # ccc"),
        };

        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.Comment, descending: false));
        list.Select(e => e.Comment).ShouldBe(["aaa", "bbb", "ccc"]);

        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.Comment, descending: true));
        list.Select(e => e.Comment).ShouldBe(["ccc", "bbb", "aaa"]);
    }

    [TestMethod]
    public void GetComparer_Enabled_Sorts()
    {
        var enabled = new HostsEntry("1.1.1.1 on");
        var disabled = new HostsEntry("# 2.2.2.2 off");
        var list = new List<HostsEntry> { enabled, disabled };

        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.Enabled, descending: false));
        list[0].Enabled.ShouldBeFalse(); // false sorts before true ascending
        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.Enabled, descending: true));
        list[0].Enabled.ShouldBeTrue();
    }

    [TestMethod]
    public void GetComparer_Valid_Sorts()
    {
        var valid = new HostsEntry("1.1.1.1 host");
        var invalid = new HostsEntry("# just a comment");
        var list = new List<HostsEntry> { valid, invalid };

        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.Valid, descending: false));
        list[0].Valid.ShouldBeFalse();
        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.Valid, descending: true));
        list[0].Valid.ShouldBeTrue();
    }

    [TestMethod]
    public void GetComparer_UnknownColumn_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => HostsEntry.GetComparer((HostsEntry.SortColumn)999, false));

    [TestMethod]
    public void IpSortKey_CompareTo_OrdersByRankThenValue()
    {
        var v4Low = new HostsEntry("1.1.1.1 a").GetIpSortKey();
        var v4High = new HostsEntry("2.2.2.2 b").GetIpSortKey();
        var v6 = new HostsEntry("::1 c").GetIpSortKey();
        var noIp = new HostsEntry("# note").GetIpSortKey();

        v4Low.CompareTo(v4High).ShouldBeLessThan(0);
        v4High.CompareTo(v4Low).ShouldBeGreaterThan(0);
        v4High.CompareTo(v6).ShouldBeLessThan(0);   // IPv4 rank before IPv6
        v6.CompareTo(noIp).ShouldBeLessThan(0);      // real IP before no-IP
        v4Low.CompareTo(v4Low).ShouldBe(0);
    }

    // ---- Property setters / undo ----

    [TestMethod]
    public void Comment_Change_RecordsUndo()
    {
        var entry = new HostsEntry("1.1.1.1 host # original");
        UndoManager.Instance.ClearHistory();

        entry.Comment = "changed";
        entry.Comment.ShouldBe("changed");
        UndoManager.Instance.CanUndo.ShouldBeTrue();

        UndoManager.Instance.Undo();
        entry.Comment.ShouldBe("original");
    }

    [TestMethod]
    public void HostNames_Change_RecordsUndo_AndTrims()
    {
        var entry = new HostsEntry("1.1.1.1 host");
        UndoManager.Instance.ClearHistory();

        entry.HostNames = "  newhost  ";
        entry.HostNames.ShouldBe("newhost");
        UndoManager.Instance.CanUndo.ShouldBeTrue();

        UndoManager.Instance.Undo();
        entry.HostNames.ShouldBe("host");
    }

    [TestMethod]
    public void Comment_NullValue_Throws() =>
        Should.Throw<ArgumentNullException>(() => new HostsEntry("1.1.1.1 h").Comment = null!);

    [TestMethod]
    public void HostNames_NullValue_Throws() =>
        Should.Throw<ArgumentNullException>(() => new HostsEntry("1.1.1.1 h").HostNames = null!);

    [TestMethod]
    public void IpAddress_NullValue_Throws() =>
        Should.Throw<ArgumentNullException>(() => new HostsEntry("1.1.1.1 h").IpAddress = null!);

    [TestMethod]
    public void UnparsedText_NullValue_Throws() =>
        Should.Throw<ArgumentNullException>(() => new HostsEntry("1.1.1.1 h").UnparsedText = null!);

    // ---- UnparsedText re-serialization ----

    [TestMethod]
    public void UnparsedText_InvalidEntry_PrefixedWithHash()
    {
        var entry = new HostsEntry("127.0.0.1 host");
        entry.HostNames = "bad host!!"; // invalid hostname -> entry becomes invalid

        entry.Valid.ShouldBeFalse();
        entry.UnparsedText.ShouldStartWith("#");
    }

    [TestMethod]
    public void UnparsedText_CommentOnly_ReserializesFromComment()
    {
        var entry = new HostsEntry("# hello");
        entry.Comment = "world";

        // Re-serialization takes the HasCommentOnly branch; an invalid (comment) line is hash-prefixed.
        var text = entry.UnparsedText;
        text.ShouldStartWith("#");
        text.ShouldContain("world");
    }

    [TestMethod]
    public void UnparsedText_Setter_InvalidatesAndRebuildsFromFields()
    {
        var entry = new HostsEntry("127.0.0.1 host");

        // The setter marks the serialized text stale, so the next read rebuilds it from the fields
        // rather than returning the assigned string verbatim.
        entry.UnparsedText = "literally anything";

        entry.UnparsedText.ShouldBe("127.0.0.1 host");
    }

    [TestMethod]
    public void Valid_Setter_UpdatesProperty()
    {
        var entry = new HostsEntry("# comment");
        entry.Valid.ShouldBeFalse();
        entry.Valid = true;
        entry.Valid.ShouldBeTrue();
    }

    // ---- IDataErrorInfo indexer ----

    [TestMethod]
    public void Indexer_ReturnsErrorForInvalidIp_EmptyOtherwise()
    {
        var entry = new HostsEntry("127.0.0.1 host");
        entry.IpAddress = "not-an-ip";

        entry["IpAddress"].ShouldBe(Resources.InvalidIPAddress);
        entry["SomethingElse"].ShouldBeEmpty();
    }

    // ---- Validation edge branches ----

    [TestMethod]
    public void ValidateHostnames_Invalid_SurfacesNonEmptyErrorMessage()
    {
        // Regression: Resources.InvalidHostnames previously had no matching .resx key and resolved to
        // null, so an invalid hostname produced a blank IDataErrorInfo message in both editions.
        var entry = new HostsEntry("127.0.0.1 host");
        entry.HostNames = "bad host!!"; // spaces/'!' are not valid hostname characters

        entry.Valid.ShouldBeFalse();
        entry["HostNames"].ShouldBe(Resources.InvalidHostnames);
        entry["HostNames"].ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public void ValidateHostnames_BlankOnDisabledEntry_IsNotAnError()
    {
        var entry = new HostsEntry("# 1.2.3.4 host"); // disabled entry
        entry.Enabled.ShouldBeFalse();

        entry.HostNames = string.Empty; // blank host on a disabled row is allowed
        entry["HostNames"].ShouldBeEmpty();
    }

    [TestMethod]
    public void ValidateIpAddress_Invalid_SetsErrorAndClearsWhenFixed()
    {
        var entry = new HostsEntry("127.0.0.1 host");

        entry.IpAddress = "999.999.999.999";
        entry.Valid.ShouldBeFalse();
        entry["IpAddress"].ShouldBe(Resources.InvalidIPAddress);

        // Fixing the address must clear the error (SetError removal path).
        entry.IpAddress = "10.0.0.1";
        entry["IpAddress"].ShouldBeEmpty();
        entry.Valid.ShouldBeTrue();
    }

    [TestMethod]
    public void ValidateIpAddress_BlankOnDisabledEntry_IsNotAnError()
    {
        var entry = new HostsEntry("# 1.2.3.4 host");
        entry.IpAddress = string.Empty;
        entry["IpAddress"].ShouldBeEmpty();
    }

    // ---- Auto-ping at parse time ----

    [TestMethod]
    public void Construction_WithAutoPingOn_DoesNotThrow()
    {
        HostsEntry.AutoPingIPAddress = true;
        var entry = new HostsEntry("127.0.0.1 localhost"); // triggers the parse-time ping branch
        entry.IpAddress.ShouldBe("127.0.0.1");
    }

    [TestMethod]
    public void SuspendAutoPing_SuppressesParseTimePing_AndDisposeIsIdempotent()
    {
        HostsEntry.AutoPingIPAddress = true;
        var scope = HostsEntry.SuspendAutoPing();
        try
        {
            var entry = new HostsEntry("127.0.0.1 localhost");
            entry.IpAddress.ShouldBe("127.0.0.1");
        }
        finally
        {
            scope.Dispose();
            scope.Dispose(); // idempotent
        }
    }

    // ---- Ping ----

    [TestMethod]
    public void Ping_InvalidIp_IsNoOp()
    {
        var entry = new HostsEntry("# just a comment"); // no parseable IP
        Should.NotThrow(entry.Ping);
        entry.PingFailed.ShouldBeFalse();
    }

    [TestMethod]
    public void Ping_ValidLoopback_DoesNotThrow()
    {
        var entry = new HostsEntry("127.0.0.1 localhost");
        Should.NotThrow(entry.Ping);
    }

    [TestMethod]
    public void SetIpAddress_WithAutoPingOn_Pings()
    {
        HostsEntry.AutoPingIPAddress = true;
        var entry = new HostsEntry("# comment");
        entry.IpAddress = "127.0.0.1"; // valid -> triggers auto-ping branch in ValidateIpAddress
        entry.IpAddress.ShouldBe("127.0.0.1");
    }

    // Opportunistic coverage of the ping-FAILURE reporting path. Pings a non-routable RFC5737
    // TEST-NET address (no external host is contacted). When ICMP is available the ping fails and the
    // failure state is asserted; where the environment blocks ICMP entirely the ping throws internally
    // and is swallowed, so we don't hard-fail — the deterministic paths above still cover the rest.
    [TestMethod]
    public void Ping_UnreachableAddress_MarksPingFailed_WhenIcmpAvailable()
    {
        var entry = new HostsEntry("192.0.2.1 unreachable.test");
        entry.Ping();

        var flipped = SpinUntil(() => entry.PingFailed, TimeSpan.FromSeconds(12));
        if (!flipped)
        {
            return; // ICMP unavailable in this environment; nothing to assert.
        }

        entry.PingFailed.ShouldBeTrue();
        entry["IpAddress"].ShouldNotBeEmpty();

        // Editing the IP clears the stale ping-failure (SetPingFailed false-direction change).
        entry.IpAddress = "127.0.0.1";
        entry.PingFailed.ShouldBeFalse();
    }

    // ---- Ping-activity marshalling to a UI SynchronizationContext ----

    [TestMethod]
    public void PingActivity_MarshalsThroughUiSynchronizationContext()
    {
        var context = new RecordingSynchronizationContext();
        HostsEntry.UiSynchronizationContext = context;
        var fired = 0;
        void Handler(object? s, EventArgs e) => fired++;
        HostsEntry.PingActivityChanged += Handler;
        try
        {
            HostsEntry.BeginPing(); // 0 -> 1 posts "started" through the context
            HostsEntry.EndPing();   // 1 -> 0 posts "stopped" through the context
            context.PostCount.ShouldBe(2);
            fired.ShouldBe(2);
        }
        finally
        {
            HostsEntry.PingActivityChanged -= Handler;
            HostsEntry.UiSynchronizationContext = null;
        }
    }

    [TestMethod]
    public void PingActivity_NoSubscriber_IsNoOp()
    {
        // No PingActivityChanged handler attached: RaisePingActivityChanged returns early.
        HostsEntry.UiSynchronizationContext = null;
        HostsEntry.BeginPing();
        HostsEntry.EndPing();
        HostsEntry.IsPingInProgress.ShouldBeFalse();
    }

    private static bool SpinUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(50);
        }

        return condition();
    }

    /// <summary>A synchronization context that runs posted callbacks inline and counts them.</summary>
    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        public int PostCount { get; private set; }

        public override void Post(SendOrPostCallback d, object? state)
        {
            PostCount++;
            d(state);
        }
    }
}
