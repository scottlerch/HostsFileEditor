using HostsFileEditor.Extensions;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class StringExtensionsTests
{
    [TestMethod]
    public void StripSpaces_RemovesSpaces() => "a b c".StripSpaces().ShouldBe("abc");

    [TestMethod]
    public void StripSpaces_NoSpaces_Unchanged() => "abc".StripSpaces().ShouldBe("abc");

    [TestMethod]
    public void StripSpaces_EmptyString() => string.Empty.StripSpaces().ShouldBeEmpty();

    [TestMethod]
    public void StripSpaces_Null_Throws() => Should.Throw<ArgumentNullException>(() => StringExtensions.StripSpaces(null!));
}
