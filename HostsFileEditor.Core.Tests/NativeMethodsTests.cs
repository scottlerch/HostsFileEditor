using HostsFileEditor.Win32;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class NativeMethodsTests
{
    [TestMethod]
    public void RegisterWindowMessage_ReturnsId()
    {
        // This will call the imported function; safe to assert >0
        var id = NativeMethods.RegisterWindowMessage("HOSTSFILEEDITOR_TEST_MESSAGE_{0}", Guid.NewGuid());
        id.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public void FlushDns_DoesNotThrow() => NativeMethods.FlushDns();// If we reach here, test passes.
}
