using HostsFileEditor.Elevation;
using HostsFileEditor.Properties;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public sealed class CoreExceptionsAndResourcesTests
{
    [TestMethod]
    public void ElevationCancelledException_DefaultMessage()
    {
        var ex = new ElevationCancelledException();
        ex.Message.ShouldContain("administrator permission");
    }

    [TestMethod]
    public void ElevationCancelledException_CustomMessage()
    {
        var ex = new ElevationCancelledException("custom");
        ex.Message.ShouldBe("custom");
    }

    [TestMethod]
    public void ElevationCancelledException_MessageAndInner()
    {
        var inner = new InvalidOperationException("inner");
        var ex = new ElevationCancelledException("outer", inner);
        ex.Message.ShouldBe("outer");
        ex.InnerException.ShouldBe(inner);
    }

    [TestMethod]
    public void HostsFileConflictException_DefaultConstructs()
    {
        var ex = new HostsFileConflictException();
        ex.ShouldNotBeNull();
    }

    [TestMethod]
    public void HostsFileConflictException_CustomMessage()
    {
        var ex = new HostsFileConflictException("conflict");
        ex.Message.ShouldBe("conflict");
    }

    [TestMethod]
    public void HostsFileConflictException_MessageAndInner()
    {
        var inner = new IOException("io");
        var ex = new HostsFileConflictException("outer", inner);
        ex.Message.ShouldBe("outer");
        ex.InnerException.ShouldBe(inner);
    }

    // Touch every strongly-typed resource accessor so the generated getters are exercised. The three
    // messages the app actually depends on for its validation/UX carry real text; the rest are read
    // for coverage without asserting a value (some are placeholder/empty in the .resx).
    [TestMethod]
    public void Resources_AllAccessorsExecute()
    {
        _ = Resources.ArchiveExists;
        _ = Resources.ErrorCaption;
        _ = Resources.InputArchivePrompt;
        _ = Resources.LoseChangesDialogCaption;
        _ = Resources.LoseChangesQuestion;
        _ = Resources.UnknownException;

        Resources.hosts.ShouldNotBeNullOrEmpty();
        Resources.InvalidHostEntries.ShouldNotBeNullOrEmpty();
        Resources.InvalidHostnames.ShouldNotBeNullOrEmpty();
        Resources.InvalidIPAddress.ShouldNotBeNullOrEmpty();
        Resources.PingFailed.ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public void Resources_CultureRoundTrips()
    {
        var original = Resources.Culture;
        try
        {
            Resources.Culture = System.Globalization.CultureInfo.InvariantCulture;
            Resources.Culture.ShouldBe(System.Globalization.CultureInfo.InvariantCulture);
            Resources.ArchiveExists.ShouldNotBeNull();
        }
        finally
        {
            Resources.Culture = original;
        }
    }
}
