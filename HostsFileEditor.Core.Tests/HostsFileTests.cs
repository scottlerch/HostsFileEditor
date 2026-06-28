using System.Reflection;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class HostsFileTests
{
    private string _tempDir = null!;
    private string _hostsFile = null!;

    [TestInitialize]
    public void Init()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _hostsFile = Path.Combine(_tempDir, "hosts");
        File.WriteAllLines(_hostsFile, ["127.0.0.1 localhost"]);
        HostsFile.TestBackupHostFilePathOverride = Path.Combine(_tempDir, "hosts.bak");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
        HostsFile.TestBackupHostFilePathOverride = null;
    }

    [TestMethod]
    public void SaveAs_WritesFile()
    {
        var hf = typeof(HostsFile).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string)], null)!
            .Invoke([_hostsFile]) as HostsFile;
        hf!.Entries.Count.ShouldBe(1);
        var newPath = Path.Combine(_tempDir, "out.txt");
        hf.SaveAs(newPath);
        File.Exists(newPath).ShouldBeTrue();
        File.Exists(HostsFile.TestBackupHostFilePathOverride!).ShouldBeTrue();
    }

    [TestMethod]
    public void Import_ReplacesEntries()
    {
        var hf = typeof(HostsFile).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string)], null)!
            .Invoke([_hostsFile]) as HostsFile;
        var importFile = Path.Combine(_tempDir, "import.txt");
        File.WriteAllLines(importFile, ["127.0.0.2 other"]);
        hf!.Import(importFile);
        hf.Entries[0].IpAddress.ShouldBe("127.0.0.2");
    }

    [TestMethod]
    public void RestoreDefault_LoadsResource()
    {
        var hf = typeof(HostsFile).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string)], null)!
            .Invoke([_hostsFile]) as HostsFile;
        hf!.RestoreDefault();
        hf.Entries.Count.ShouldBeGreaterThan(0);
    }
}
