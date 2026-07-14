using HostsFileEditor.Elevation;

namespace HostsFileEditor.Core.Tests;

/// <summary>
/// Coverage for <see cref="HostsFile"/> statics and behavior that only make sense against the process
/// singleton (bound to a temp file by <see cref="TestAssemblyInit"/>): the override plumbing,
/// preloading, and the disable-conflict guard.
/// </summary>
[TestClass]
public sealed class HostsFileSingletonGapTests
{
    [TestInitialize]
    public void Init()
    {
        PrivilegedFileOperations.Current = new InProcessPrivilegedFileOperations();

        // Re-enable first (moves the disabled copy back to live), THEN clear any leftover disabled file.
        if (!HostsFile.IsEnabled)
        {
            HostsFile.Instance.EnableHostsFile();
        }

        if (File.Exists(HostsFile.DefaultDisabledHostFilePath))
        {
            File.Delete(HostsFile.DefaultDisabledHostFilePath);
        }

        File.WriteAllLines(HostsFile.DefaultHostFilePath, TestAssemblyInit.SeedLines);
        HostsFile.Instance.Refresh();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Re-enable first so the move has a source, then remove any stray disabled copy.
        if (!HostsFile.IsEnabled)
        {
            HostsFile.Instance.EnableHostsFile();
        }

        if (File.Exists(HostsFile.DefaultDisabledHostFilePath))
        {
            File.Delete(HostsFile.DefaultDisabledHostFilePath);
        }
    }

    [TestMethod]
    public void OverridePath_IsActiveDuringTests()
    {
        HostsFile.OverridePath.ShouldNotBeNull();
        HostsFile.DefaultHostFilePath.ShouldBe(HostsFile.OverridePath);
        HostsFile.DefaultDisabledHostFilePath.ShouldEndWith(".disabled");
    }

    [TestMethod]
    public void IsEnabled_TrueWhenLiveFilePresent()
    {
        HostsFile.IsEnabled.ShouldBeTrue();
    }

    [TestMethod]
    public async Task PreloadAsync_Completes()
    {
        await HostsFile.PreloadAsync();
        HostsFile.Instance.Entries.Count.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public void DisableHostsFile_DifferentDisabledCopyExists_ThrowsConflict()
    {
        // A pre-existing, DIFFERENT hosts.disabled must not be silently overwritten (issue #99).
        File.WriteAllLines(
            HostsFile.DefaultDisabledHostFilePath,
            ["10.9.9.9 curated.test", "10.9.9.10 more.test"]);

        Should.Throw<HostsFileConflictException>(() => HostsFile.Instance.DisableHostsFile());

        // The guard fired before moving anything: the live file is intact and still enabled.
        HostsFile.IsEnabled.ShouldBeTrue();
    }

    [TestMethod]
    public void DisableHostsFile_IdenticalDisabledCopy_Succeeds()
    {
        // A byte-identical hosts.disabled is safe to overwrite, so disabling proceeds.
        File.WriteAllLines(HostsFile.DefaultDisabledHostFilePath, TestAssemblyInit.SeedLines);

        HostsFile.Instance.DisableHostsFile();

        HostsFile.IsEnabled.ShouldBeFalse();
    }
}
