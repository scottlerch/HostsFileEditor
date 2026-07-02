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
