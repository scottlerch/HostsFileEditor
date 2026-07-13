namespace HostsFileEditor.Core.Tests;

[TestClass]
public class HostsArchiveListTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Init()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        HostsArchiveList.TestArchiveDirectoryOverride = _tempDir;
    }

    [TestCleanup]
    public void Cleanup()
    {
        HostsArchiveList.TestArchiveDirectoryOverride = null;
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [TestMethod]
    public void Refresh_LoadsFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "b.txt"), "y");
        HostsArchiveList.Instance.Refresh();
        HostsArchiveList.Instance.Count.ShouldBe(2);
    }

    [TestMethod]
    public void FindByName_MatchesCaseInsensitively()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyHosts1"), "x");
        File.WriteAllText(Path.Combine(_tempDir, "Default"), "y");
        HostsArchiveList.Instance.Refresh();

        HostsArchiveList.Instance.FindByName("MyHosts1").ShouldNotBeNull();
        HostsArchiveList.Instance.FindByName("myhosts1")!.FileName.ShouldBe("MyHosts1");
        HostsArchiveList.Instance.FindByName("DEFAULT")!.FileName.ShouldBe("Default");
    }

    [TestMethod]
    public void FindByName_ReturnsNull_WhenMissing()
    {
        File.WriteAllText(Path.Combine(_tempDir, "MyHosts1"), "x");
        HostsArchiveList.Instance.Refresh();

        HostsArchiveList.Instance.FindByName("nope").ShouldBeNull();
    }

    [TestMethod]
    public void Delete_RemovesFile()
    {
        var path = Path.Combine(_tempDir, "a.txt");
        File.WriteAllText(path, "x");
        HostsArchiveList.Instance.Refresh();
        var item = HostsArchiveList.Instance.First(a => a.FilePath == path);
        HostsArchiveList.Instance.Delete(item);
        File.Exists(path).ShouldBeFalse();
    }
}
