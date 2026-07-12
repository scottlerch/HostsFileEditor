using HostsFileEditor.Utilities;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class HostsEntryTests
{
    [TestMethod]
    public void Parse_ValidEntry()
    {
        var entry = new HostsEntry("127.0.0.1 localhost # comment");
        entry.Valid.ShouldBeTrue();
        entry.Enabled.ShouldBeTrue();
        entry.IpAddress.ShouldBe("127.0.0.1");
        entry.HostNames.ShouldBe("localhost");
        entry.Comment.ShouldBe("comment");
    }

    [TestMethod]
    public void SettingPropertyToSameValue_DoesNotRecordUndo()
    {
        var entry = new HostsEntry("127.0.0.1 localhost");
        var currentEnabled = entry.Enabled;
        var currentComment = entry.Comment;
        UndoManager.Instance.ClearHistory();

        // Re-assigning the current value of each property must not pollute undo history.
        entry.IpAddress = "127.0.0.1";
        entry.HostNames = "localhost";
        entry.Enabled = currentEnabled;
        entry.Comment = currentComment;
        UndoManager.Instance.CanUndo.ShouldBeFalse();

        // A genuine change is still recorded.
        entry.IpAddress = "10.0.0.1";
        UndoManager.Instance.CanUndo.ShouldBeTrue();

        UndoManager.Instance.ClearHistory();
    }

    [TestMethod]
    public void Parse_DisabledEntry()
    {
        var entry = new HostsEntry("# 127.0.0.1 localhost");
        entry.Enabled.ShouldBeFalse();
        entry.Valid.ShouldBeTrue();
    }

    [TestMethod]
    public void Parse_CommentOnly()
    {
        var entry = new HostsEntry("# just a comment line");
        entry.HasCommentOnly.ShouldBeTrue();
        entry.Valid.ShouldBeFalse();
    }

    [TestMethod]
    public void Change_IpAddress_InvalidatesText()
    {
        var entry = new HostsEntry("127.0.0.1 localhost");
        var original = entry.UnparsedText;
        entry.IpAddress = "127.0.0.2";
        entry.UnparsedText.ShouldNotBe(original);
        entry.IpAddress.ShouldBe("127.0.0.2");
    }

    [TestMethod]
    public void Invalid_IpAddress()
    {
        var entry = new HostsEntry("256.256.256.256 localhost");
        entry.Valid.ShouldBeFalse();
    }

    [TestMethod]
    public void Invalid_Hostname_AfterEdit()
    {
        var entry = new HostsEntry("127.0.0.1 goodhost");
        entry.Valid.ShouldBeTrue();
        entry.HostNames = "bad@host"; // invalid character
        entry.Valid.ShouldBeFalse();
    }

    [TestMethod]
    public void ToString_ReturnsExpected()
    {
        var entry = new HostsEntry("127.0.0.1 localhost # c");
        entry.ToString().ShouldBe("127.0.0.1 localhost c");
    }

    [TestMethod]
    public void GetComparer_SortsByColumnAndDirection()
    {
        var a = new HostsEntry("1.1.1.1 alpha.com");
        var b = new HostsEntry("2.2.2.2 bravo.com");
        var c = new HostsEntry("3.3.3.3 charlie.com");
        var list = new List<HostsEntry> { c, a, b };

        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.HostNames, descending: false));
        list.Select(e => e.HostNames).ShouldBe(["alpha.com", "bravo.com", "charlie.com"]);

        list.Sort(HostsEntry.GetComparer(HostsEntry.SortColumn.IpAddress, descending: true));
        list.Select(e => e.IpAddress).ShouldBe(["3.3.3.3", "2.2.2.2", "1.1.1.1"]);
    }

    [TestMethod]
    public void GetComparer_DoesNotMutateOriginalOrder_WhenUsedForStableCopy()
    {
        var a = new HostsEntry("127.0.0.1 a") { };
        var b = new HostsEntry("127.0.0.1 b") { };
        var source = new List<HostsEntry> { b, a };

        // A display sort must be non-destructive: sorting a COPY leaves the source order untouched.
        var sorted = source.OrderBy(e => e, HostsEntry.GetComparer(HostsEntry.SortColumn.HostNames, false)).ToList();

        sorted.Select(e => e.HostNames).ShouldBe(["a", "b"]);
        source.Select(e => e.HostNames).ShouldBe(["b", "a"]);
    }

    [TestMethod]
    public void MatchesFilter_HideComments_HidesOnlyCommentOnlyLines()
    {
        var comment = new HostsEntry("# just a comment");
        var entry = new HostsEntry("127.0.0.1 localhost");

        // hideComments off: everything passes.
        HostsEntry.MatchesFilter(comment, hideComments: false, hideDisabled: false, null).ShouldBeTrue();
        // hideComments on: comment-only lines are hidden, real entries stay.
        HostsEntry.MatchesFilter(comment, hideComments: true, hideDisabled: false, null).ShouldBeFalse();
        HostsEntry.MatchesFilter(entry, hideComments: true, hideDisabled: false, null).ShouldBeTrue();
    }

    [TestMethod]
    public void MatchesFilter_HideDisabled_HidesDisabledEntriesButNotComments()
    {
        var disabled = new HostsEntry("# 127.0.0.1 localhost"); // disabled entry (valid IP)
        var comment = new HostsEntry("# just a comment");        // comment-only
        var enabled = new HostsEntry("127.0.0.1 localhost");

        disabled.Enabled.ShouldBeFalse();
        disabled.HasCommentOnly.ShouldBeFalse();

        HostsEntry.MatchesFilter(disabled, hideComments: false, hideDisabled: true, null).ShouldBeFalse();
        // A comment-only line is NOT a "disabled entry" — hideDisabled must leave it visible.
        HostsEntry.MatchesFilter(comment, hideComments: false, hideDisabled: true, null).ShouldBeTrue();
        HostsEntry.MatchesFilter(enabled, hideComments: false, hideDisabled: true, null).ShouldBeTrue();
    }

    [TestMethod]
    public void MatchesFilter_Text_IsCaseInsensitiveAndSubstring()
    {
        var entry = new HostsEntry("127.0.0.1 Ads.Example.COM");

        HostsEntry.MatchesFilter(entry, false, false, "ads.example").ShouldBeTrue();   // case-insensitive
        HostsEntry.MatchesFilter(entry, false, false, "ADS").ShouldBeTrue();
        HostsEntry.MatchesFilter(entry, false, false, "127.0.0.1").ShouldBeTrue();     // matches IP too
        HostsEntry.MatchesFilter(entry, false, false, "nomatch").ShouldBeFalse();
        HostsEntry.MatchesFilter(entry, false, false, "").ShouldBeTrue();              // empty matches all
        HostsEntry.MatchesFilter(entry, false, false, null).ShouldBeTrue();
    }

    [TestMethod]
    public void MatchesFilter_NullEntry_Throws() =>
        Should.Throw<ArgumentNullException>(() => HostsEntry.MatchesFilter(null!, false, false, null));

    [TestMethod]
    public void PingActivity_RaisesOnZeroBoundaryOnly()
    {
        var events = 0;
        void Handler(object? s, EventArgs e) => events++;
        HostsEntry.PingActivityChanged += Handler;
        try
        {
            HostsEntry.IsPingInProgress.ShouldBeFalse();

            HostsEntry.BeginPing();               // 0 -> 1: fires "started"
            HostsEntry.IsPingInProgress.ShouldBeTrue();
            events.ShouldBe(1);

            HostsEntry.BeginPing();               // 1 -> 2: no fire
            events.ShouldBe(1);
            HostsEntry.IsPingInProgress.ShouldBeTrue();

            HostsEntry.EndPing();                 // 2 -> 1: no fire
            events.ShouldBe(1);
            HostsEntry.IsPingInProgress.ShouldBeTrue();

            HostsEntry.EndPing();                 // 1 -> 0: fires "stopped"
            events.ShouldBe(2);
            HostsEntry.IsPingInProgress.ShouldBeFalse();
        }
        finally
        {
            HostsEntry.PingActivityChanged -= Handler;
        }
    }

    [TestMethod]
    public void CloneConstructor_CopiesValues()
    {
        var entry = new HostsEntry("127.0.0.1 localhost # hi");
        var clone = new HostsEntry(entry);
        clone.IpAddress.ShouldBe(entry.IpAddress);
        clone.HostNames.ShouldBe(entry.HostNames);
        clone.Comment.ShouldBe(entry.Comment);
        clone.Valid.ShouldBe(entry.Valid);
        clone.Enabled.ShouldBe(entry.Enabled);
    }

    [TestMethod]
    public void Parse_MultipleHostnames()
    {
        var entry = new HostsEntry("1.2.3.4 host1 host2 host3");
        entry.Valid.ShouldBeTrue();
        entry.IpAddress.ShouldBe("1.2.3.4");
        entry.HostNames.ShouldBe("host1 host2 host3");
    }

    [TestMethod]
    public void Parse_FqdnTrailingDot_IsValid()
    {
        // Tailscale MagicDNS writes every entry as a fully-qualified name ending in a root dot.
        // The trailing dot must not demote the line to a comment, and it must round-trip.
        const string raw = "100.64.0.1 host.tailnet.ts.net. host";
        var entry = new HostsEntry(raw);
        entry.Valid.ShouldBeTrue();
        entry.IpAddress.ShouldBe("100.64.0.1");
        entry.HostNames.ShouldBe("host.tailnet.ts.net. host");
        entry.UnparsedText.ShouldBe(raw);
    }

    [TestMethod]
    public void Parse_FqdnTrailingDot_WithAfterComment()
    {
        var entry = new HostsEntry("100.64.0.1 host.tailnet.ts.net. host # note");
        entry.Valid.ShouldBeTrue();
        entry.IpAddress.ShouldBe("100.64.0.1");
        entry.HostNames.ShouldBe("host.tailnet.ts.net. host");
        entry.Comment.ShouldBe("note");
    }

    [TestMethod]
    public void Parse_FqdnTrailingDot_IPv6()
    {
        // Tailscale MagicDNS also writes AAAA entries in the fd7a:115c::/48 range.
        var entry = new HostsEntry("fd7a:115c:a1e0::1 host.tailnet.ts.net. host");
        entry.Valid.ShouldBeTrue();
        entry.IpAddress.ShouldBe("fd7a:115c:a1e0::1");
        entry.HostNames.ShouldBe("host.tailnet.ts.net. host");
    }

    [TestMethod]
    [DataRow("# TailscaleHostsSectionStart")]
    [DataRow("# This section contains MagicDNS entries for Tailscale.")]
    [DataRow("# Do not edit this section manually.")]
    public void Parse_ProseComment_EndingInDot_StaysComment(string raw)
    {
        // Prose comment lines that happen to end in '.' must not be captured as (invalid)
        // disabled entries now that hostnames accept a trailing dot. The leading '#' with a
        // non-IP first token must keep them comment-only and round-trip verbatim.
        var entry = new HostsEntry(raw);
        entry.HasCommentOnly.ShouldBeTrue();
        entry.Valid.ShouldBeFalse();
        entry.Enabled.ShouldBeFalse();
        entry.UnparsedText.ShouldBe(raw);
    }

    [TestMethod]
    [DataRow("# 8.8.8.8 use for dns.")]    // trailing dot: a comment pre-#66, a disabled entry now
    [DataRow("# 8.8.8.8 use for dns")]     // no trailing dot: a disabled entry since before #66
    [DataRow("# 0.0.0.0 blocks all ads.")]
    public void Parse_DisabledEntry_LeadingValidIp_IsEntryNotComment(string raw)
    {
        // The other side of the boundary from Parse_ProseComment_EndingInDot_StaysComment: a
        // '#'-prefixed line whose content is a syntactically valid hosts entry — a REAL leading IP
        // plus hostname tokens — parses as a DISABLED entry, not a comment. Only the invalid-IP prose
        // in that test stays a comment (its first token is demoted); a valid leading IP does not.
        // The no-dot form has behaved this way since before #66; #66 widened the hostname to accept a
        // trailing FQDN root dot, so the period-terminated form now matches too — an FQDN entry and a
        // period-terminated prose line beginning with an IP are indistinguishable to the parser.
        // Pinned so the boundary is documented rather than a surprise; it round-trips verbatim, so no
        // silent corruption. See issue #80 for the strict-IP-validation follow-up idea.
        var entry = new HostsEntry(raw);
        entry.HasCommentOnly.ShouldBeFalse();
        entry.Enabled.ShouldBeFalse();
        entry.Valid.ShouldBeTrue();
        entry.UnparsedText.ShouldBe(raw);
    }

    [TestMethod]
    [DataRow("999.999.999.999 host.example.com")]  // octets out of range
    [DataRow("notanip host.example.com")]           // not an address at all
    [DataRow("192.168.1.1:8080 host.example.com")]  // address with a port
    [DataRow("# 999.999.999.999 host.example.com")] // same, disabled form
    [DataRow("# notanip host.example.com")]
    public void Parse_StructuralEntryWithInvalidIp_IsCommentNotEntry(string raw)
    {
        // Issue #80: entry-ness is gated on a syntactically valid IP first token. A line that is
        // structurally shaped like an entry ("<token> <valid-hostnames>") but whose first token is
        // not a real IP is a comment, not a (would-be) disabled entry — and it round-trips verbatim.
        var entry = new HostsEntry(raw);
        entry.HasCommentOnly.ShouldBeTrue();
        entry.Valid.ShouldBeFalse();
        entry.Enabled.ShouldBeFalse();
        entry.IpAddress.ShouldBeEmpty();
        entry.HostNames.ShouldBeEmpty();
        entry.UnparsedText.ShouldBe(raw);
    }

    [TestMethod]
    [DataRow("127.0.0.1 host.example.com", "127.0.0.1")]
    [DataRow("::1 localhost", "::1")]
    [DataRow("fe80::1 host.example.com", "fe80::1")]
    [DataRow("2001:db8::1 host.example.com", "2001:db8::1")]
    [DataRow("fe80::1%eth0 host.example.com", "fe80::1%eth0")] // zone / scope id
    public void Parse_ValidIpForms_StayEntries(string raw, string expectedIp)
    {
        // The strict-IP gate must accept every IP form the framework does — all IPv6 shapes, zone
        // ids — so these are still parsed as entries, not demoted to comments.
        var entry = new HostsEntry(raw);
        entry.Valid.ShouldBeTrue();
        entry.IpAddress.ShouldBe(expectedIp);
    }

    [TestMethod]
    public void Parse_IPv6()
    {
        var entry = new HostsEntry("::1 localhost");
        entry.Valid.ShouldBeTrue();
        entry.IpAddress.ShouldBe("::1");
        entry.HostNames.ShouldBe("localhost");
    }

    [TestMethod]
    public void Parse_TabSeparated()
    {
        var entry = new HostsEntry("127.0.0.1\tlocalhost");
        entry.Valid.ShouldBeTrue();
        entry.IpAddress.ShouldBe("127.0.0.1");
        entry.HostNames.ShouldBe("localhost");
    }

    [TestMethod]
    public void Parse_TrailingWhitespace_StillValid()
    {
        var entry = new HostsEntry("127.0.0.1 localhost   ");
        entry.Valid.ShouldBeTrue();
        entry.HostNames.ShouldBe("localhost");
    }

    [TestMethod]
    public void Parse_LeadingWhitespace_DisabledEntry()
    {
        // Issue #22: a space before the '#' must not break parsing — the line is still a
        // disabled host entry, not a plain comment.
        var entry = new HostsEntry(" # 127.0.0.1  localhost");
        entry.Valid.ShouldBeTrue();
        entry.Enabled.ShouldBeFalse();
        entry.IpAddress.ShouldBe("127.0.0.1");
        entry.HostNames.ShouldBe("localhost");
    }

    [TestMethod]
    public void Parse_LeadingWhitespace_EnabledEntry()
    {
        // Leading whitespace before an enabled entry likewise parses normally.
        var entry = new HostsEntry("   127.0.0.1 localhost");
        entry.Valid.ShouldBeTrue();
        entry.Enabled.ShouldBeTrue();
        entry.IpAddress.ShouldBe("127.0.0.1");
        entry.HostNames.ShouldBe("localhost");
    }

    [TestMethod]
    public void Parse_LeadingTab_DisabledEntry()
    {
        var entry = new HostsEntry("\t# 10.0.0.1 example.com");
        entry.Valid.ShouldBeTrue();
        entry.Enabled.ShouldBeFalse();
        entry.IpAddress.ShouldBe("10.0.0.1");
        entry.HostNames.ShouldBe("example.com");
    }

    [TestMethod]
    public void Parse_DisabledWithAfterComment()
    {
        var entry = new HostsEntry("# 127.0.0.1 localhost # note");
        entry.Enabled.ShouldBeFalse();
        entry.Valid.ShouldBeTrue();
        entry.IpAddress.ShouldBe("127.0.0.1");
        entry.HostNames.ShouldBe("localhost");
        entry.Comment.ShouldBe("note");
    }

    [TestMethod]
    public void Parse_MalformedLine_PreservedAsComment_RoundTrips()
    {
        // A hostname with a port is not valid hosts syntax. The line must be preserved
        // verbatim (surfaced as a comment) rather than silently parsed as "localhost" with
        // ":8080" dropped, and it must round-trip unchanged.
        const string raw = "127.0.0.1 localhost:8080";
        var entry = new HostsEntry(raw);
        entry.Valid.ShouldBeFalse();
        entry.Comment.ShouldContain("localhost:8080");
        entry.UnparsedText.ShouldBe(raw);
    }

    [TestMethod]
    public void DisabledEntry_WithBlankIp_IsValid()
    {
        var entry = new HostsEntry("127.0.0.1 host");
        entry.Enabled = false;
        entry.IpAddress = string.Empty;

        // A disabled entry being edited with a blank IP is not an error.
        entry.Valid.ShouldBeTrue();
    }

    [TestMethod]
    public void Enabled_Toggle()
    {
        var entry = new HostsEntry("127.0.0.1 localhost");
        entry.Enabled.ShouldBeTrue();
        entry.Enabled = false;
        entry.Enabled.ShouldBeFalse();
        entry.UnparsedText.ShouldStartWith("#");
    }

    [TestMethod]
    public void Comment_Update()
    {
        var entry = new HostsEntry("127.0.0.1 localhost")
        {
            Comment = "abc"
        };
        entry.UnparsedText.ShouldContain("# abc");
    }
}
