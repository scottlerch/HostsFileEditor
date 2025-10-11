namespace HostsFileEditor.Core.Tests;

[TestClass]
public class ProgramInfoTests
{
    [TestMethod]
    public void AssemblyGuid_Format()
    {
        Guid.TryParse(ProgramInfo.AssemblyGuid, out var g).ShouldBeTrue();
    }
}
