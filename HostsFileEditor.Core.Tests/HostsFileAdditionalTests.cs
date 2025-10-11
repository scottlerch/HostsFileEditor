using System.Reflection;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class HostsFileAdditionalTests
{
    private string _tempDir = null!;
    private string _tempFile = null!;

    [TestInitialize]
    public void Init()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _tempFile = Path.Combine(_tempDir, "hosts");
        File.WriteAllLines(_tempFile, new[]{"127.0.0.1 localhost"});
        HostsFile.TestBackupHostFilePathOverride = Path.Combine(_tempDir, "hosts.bak");
        HostsArchiveList.TestArchiveDirectoryOverride = _tempDir; // ensure archives stored in temp
    }

    [TestCleanup]
    public void Cleanup()
    {
        HostsFile.TestBackupHostFilePathOverride = null;
        HostsArchiveList.TestArchiveDirectoryOverride = null;
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    private HostsFile Create() => (HostsFile)typeof(HostsFile)
        .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[]{typeof(string)}, null)!
        .Invoke(new object[]{_tempFile});

    [TestMethod]
    public void EnabledCountAndLineCount_UpdateOnListChange()
    {
        var hf = Create();
        var initialEnabled = hf.EnabledCount;
        hf.Entries.Add(new HostsEntry("127.0.0.2 testhost"));
        hf.EnabledCount.ShouldBe(initialEnabled + 1);
        hf.LineCount.ShouldBe(hf.Entries.Count);
    }

    [TestMethod]
    public void Refresh_ReloadsEntries()
    {
        var hf = Create();
        File.WriteAllLines(_tempFile, new[]{"127.0.0.9 changed"});
        hf.Refresh();
        hf.Entries.First().IpAddress.ShouldBe("127.0.0.9");
    }

    [TestMethod]
    public void Import_SameFile_NoChange()
    {
        var hf = Create();
        var before = hf.LineCount;
        hf.Import(_tempFile); // same path; should be no change
        hf.LineCount.ShouldBe(before);
    }

    [TestMethod]
    public void Archive_CreatesArchiveEntry()
    {
        var hf = Create();
        var archiveName = Guid.NewGuid().ToString()+".txt";
        hf.Archive(archiveName);
        File.Exists(Path.Combine(_tempDir, archiveName)).ShouldBeTrue();
        HostsArchiveList.Instance.Any(a => a.FilePath.EndsWith(archiveName)).ShouldBeTrue();
    }
}
