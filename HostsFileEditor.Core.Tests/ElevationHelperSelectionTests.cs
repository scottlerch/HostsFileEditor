using HostsFileEditor.Elevation;

namespace HostsFileEditor.Core.Tests;

/// <summary>
/// Coverage for <see cref="PrivilegedFileOperations.UseElevationHelper"/>'s helper-resolution branches.
/// <see cref="PrivilegedFileOperations.Current"/> is process-global, so every test restores it.
/// </summary>
[TestClass]
public sealed class ElevationHelperSelectionTests
{
    private IPrivilegedFileOperations _originalCurrent = null!;

    [TestInitialize]
    public void Init() => _originalCurrent = PrivilegedFileOperations.Current;

    [TestCleanup]
    public void Cleanup() => PrivilegedFileOperations.Current = _originalCurrent;

    [TestMethod]
    public void UseElevationHelper_ExplicitExistingPath_SelectsHelper()
    {
        var helper = Path.Combine(Path.GetTempPath(), "HfeHelper_" + Guid.NewGuid().ToString("N") + ".exe");
        File.WriteAllText(helper, "not a real exe");
        try
        {
            PrivilegedFileOperations.Current = new InProcessPrivilegedFileOperations();
            PrivilegedFileOperations.UseElevationHelper(helper);
            PrivilegedFileOperations.Current.ShouldBeOfType<ElevatedHelperPrivilegedFileOperations>();
        }
        finally
        {
            File.Delete(helper);
        }
    }

    [TestMethod]
    public void UseElevationHelper_ExplicitMissingPath_LeavesCurrentUnchanged()
    {
        var inProcess = new InProcessPrivilegedFileOperations();
        PrivilegedFileOperations.Current = inProcess;

        PrivilegedFileOperations.UseElevationHelper(Path.Combine(Path.GetTempPath(), "no-such-helper.exe"));

        PrivilegedFileOperations.Current.ShouldBeSameAs(inProcess);
    }

    [TestMethod]
    public void UseElevationHelper_NoHelperBesideApp_StaysInProcess()
    {
        var inProcess = new InProcessPrivilegedFileOperations();
        PrivilegedFileOperations.Current = inProcess;

        // No HostsFileEditor.Elevate.exe ships in the test bin, so the default probe finds nothing.
        PrivilegedFileOperations.UseElevationHelper();

        PrivilegedFileOperations.Current.ShouldBeSameAs(inProcess);
    }

    [TestMethod]
    public void UseElevationHelper_HelperBesideApp_SelectsHelper()
    {
        // Drop a stand-in helper next to the test assembly so the default candidate probe finds it.
        var candidate = Path.Combine(AppContext.BaseDirectory, PrivilegedFileOperations.HelperExecutableName);
        var created = !File.Exists(candidate);
        if (created)
        {
            File.WriteAllText(candidate, "stub");
        }

        try
        {
            PrivilegedFileOperations.Current = new InProcessPrivilegedFileOperations();
            PrivilegedFileOperations.UseElevationHelper();
            PrivilegedFileOperations.Current.ShouldBeOfType<ElevatedHelperPrivilegedFileOperations>();
        }
        finally
        {
            if (created)
            {
                File.Delete(candidate);
            }
        }
    }
}
