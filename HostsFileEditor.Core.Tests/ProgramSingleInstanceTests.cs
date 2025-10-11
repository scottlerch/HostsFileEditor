namespace HostsFileEditor.Core.Tests;

[TestClass]
public class ProgramSingleInstanceTests
{
    [TestMethod]
    public void Start_ReturnsInstance()
    {
        using var inst = ProgramSingleInstance.Start();
        inst.ShouldNotBeNull();
    }
}
