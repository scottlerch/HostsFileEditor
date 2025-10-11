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
        var entry = new HostsEntry("127.0.0.1 localhost");
        entry.Comment = "abc";
        entry.UnparsedText.ShouldContain("# abc");
    }
}
