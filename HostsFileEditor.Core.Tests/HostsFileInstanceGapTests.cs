using System.Reflection;

namespace HostsFileEditor.Core.Tests;

/// <summary>
/// Coverage for <see cref="HostsFile"/> instance behavior that can be driven through the private
/// constructor (no singleton / no live hosts file involved).
/// </summary>
[TestClass]
public sealed class HostsFileInstanceGapTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Init()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        HostsFile.TestBackupHostFilePathOverride = Path.Combine(_tempDir, "hosts.bak");
    }

    [TestCleanup]
    public void Cleanup()
    {
        HostsFile.TestBackupHostFilePathOverride = null;
        if (Directory.Exists(_tempDir))
        {
            foreach (var file in Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }

            Directory.Delete(_tempDir, true);
        }
    }

    private static HostsFile Create(string path) => (HostsFile)typeof(HostsFile)
        .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string)], null)!
        .Invoke([path]);

    [TestMethod]
    public void Constructor_MissingFile_StartsWithEmptyEntries()
    {
        var hf = Create(Path.Combine(_tempDir, "does-not-exist"));
        hf.Entries.Count.ShouldBe(0);
        hf.LineCount.ShouldBe(0);
        hf.EnabledCount.ShouldBe(0);
    }

    [TestMethod]
    public void SaveAs_DriveRoot_ThrowsArgumentException()
    {
        var file = Path.Combine(_tempDir, "hosts");
        File.WriteAllLines(file, ["127.0.0.1 localhost"]);
        var hf = Create(file);

        var root = Directory.GetDirectoryRoot(_tempDir); // e.g. "C:\" — DirectoryName is null

        Should.Throw<ArgumentException>(() => hf.SaveAs(root));
    }

    [TestMethod]
    public void SaveAs_CreatesMissingDirectory()
    {
        var file = Path.Combine(_tempDir, "hosts");
        File.WriteAllLines(file, ["127.0.0.1 localhost"]);
        var hf = Create(file);

        var nested = Path.Combine(_tempDir, "new", "sub", "out.txt");
        hf.SaveAs(nested);

        File.Exists(nested).ShouldBeTrue();
    }

    [TestMethod]
    public void Refresh_RemoveDefaultFalse_KeepsDefaultLines()
    {
        var file = Path.Combine(_tempDir, "hosts");
        File.WriteAllLines(file, HostsEntryList.DefaultLines);
        var hf = Create(file);

        hf.Refresh(removeDefault: false);

        hf.Entries.Count.ShouldBe(HostsEntryList.DefaultLines.Length);
    }

    [TestMethod]
    public void RestoreDefault_LoadsBundledDefault_AndMarksModified()
    {
        var file = Path.Combine(_tempDir, "hosts");
        File.WriteAllLines(file, ["127.0.0.1 localhost"]);
        var hf = Create(file);

        hf.RestoreDefault();

        hf.Entries.Count.ShouldBeGreaterThan(0);
        hf.IsModified.ShouldBeTrue(); // RestoreDefault deliberately leaves the model "needs save"
    }

    [TestMethod]
    public void Merge_AddsNonDuplicateEntries()
    {
        var file = Path.Combine(_tempDir, "hosts");
        File.WriteAllLines(file, ["127.0.0.1 localhost"]);
        var hf = Create(file);

        var mergeFile = Path.Combine(_tempDir, "merge.txt");
        File.WriteAllLines(mergeFile, ["127.0.0.1 localhost", "9.9.9.9 quad9.test"]);

        var added = hf.Merge(mergeFile);

        added.ShouldBe(1);
        hf.Entries.ShouldContain(e => e.HostNames == "quad9.test");
    }

    [TestMethod]
    public void DisableWouldOverwriteDifferentFile_UnreadableFile_TreatedAsConflict()
    {
        var live = Path.Combine(_tempDir, "hosts");
        var disabled = Path.Combine(_tempDir, "hosts.disabled");
        File.WriteAllText(live, "aaaa");
        File.WriteAllText(disabled, "bbbb"); // same length so the compare opens the stream

        // Lock the live file so the content compare throws IOException, which the guard treats as a
        // conflict (err on the side of not destroying data).
        using var _ = new FileStream(live, FileMode.Open, FileAccess.Read, FileShare.None);

        HostsFile.DisableWouldOverwriteDifferentFile(live, disabled).ShouldBeTrue();
    }

    [TestMethod]
    public void IsModified_FlipsWithEditsAndSave()
    {
        var file = Path.Combine(_tempDir, "hosts");
        File.WriteAllLines(file, ["127.0.0.1 localhost"]);
        var hf = Create(file);

        hf.IsModified.ShouldBeFalse();

        hf.Entries.Add(new HostsEntry("10.0.0.1 added.test"));
        hf.IsModified.ShouldBeTrue();

        hf.Save();
        hf.IsModified.ShouldBeFalse();
    }
}
