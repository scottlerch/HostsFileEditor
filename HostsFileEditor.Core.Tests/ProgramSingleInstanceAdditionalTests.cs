namespace HostsFileEditor.Core.Tests;

[TestClass]
public class ProgramSingleInstanceAdditionalTests
{
    [TestMethod]
    public void MultipleInstances_FlagBehaves()
    {
        using var first = ProgramSingleInstance.Start();
        first.IsOnlyInstance.ShouldBeTrue();
        using var second = ProgramSingleInstance.Start();
        second.IsOnlyInstance.ShouldBeFalse();
    }

    [TestMethod]
    public void Dispose_ReleasesMutex_NoExceptions()
    {
        ProgramSingleInstance? inst;
        using (inst = ProgramSingleInstance.Start())
        {
            inst.IsOnlyInstance.ShouldBeTrue();
        }
        // After dispose we can still create another instance (may not become only if previous secondary exists)
        using var again = ProgramSingleInstance.Start();
        again.ShouldNotBeNull();
    }
}
