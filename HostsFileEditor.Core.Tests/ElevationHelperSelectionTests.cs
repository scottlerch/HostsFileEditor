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
        // No HostsFileEditor.Elevate.exe ships in the test bin. Defensively clear any stub a prior
        // (interrupted) run of UseElevationHelper_HelperBesideApp_SelectsHelper may have left behind,
        // so the default probe genuinely finds nothing regardless of test order.
        foreach (var stray in HelperCandidatePaths())
        {
            if (File.Exists(stray))
            {
                File.Delete(stray);
            }
        }

        var inProcess = new InProcessPrivilegedFileOperations();
        PrivilegedFileOperations.Current = inProcess;

        PrivilegedFileOperations.UseElevationHelper();

        PrivilegedFileOperations.Current.ShouldBeSameAs(inProcess);
    }

    [TestMethod]
    public void UseElevationHelper_HelperBesideApp_SelectsHelper()
    {
        // Drop a stand-in helper next to the test assembly so the default candidate probe finds it.
        // The test bin never contains a real helper, so the stub is always removed afterwards.
        var candidate = Path.Combine(AppContext.BaseDirectory, PrivilegedFileOperations.HelperExecutableName);
        File.WriteAllText(candidate, "stub");

        try
        {
            PrivilegedFileOperations.Current = new InProcessPrivilegedFileOperations();
            PrivilegedFileOperations.UseElevationHelper();
            PrivilegedFileOperations.Current.ShouldBeOfType<ElevatedHelperPrivilegedFileOperations>();
        }
        finally
        {
            File.Delete(candidate);
        }
    }

    private static IEnumerable<string> HelperCandidatePaths() =>
    [
        Path.Combine(AppContext.BaseDirectory, PrivilegedFileOperations.HelperSubdirectory, PrivilegedFileOperations.HelperExecutableName),
        Path.Combine(AppContext.BaseDirectory, PrivilegedFileOperations.HelperExecutableName),
    ];
}
