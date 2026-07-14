using HostsFileEditor.Utilities;
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
        File.WriteAllLines(_tempFile, ["127.0.0.1 localhost"]);
        HostsFile.TestBackupHostFilePathOverride = Path.Combine(_tempDir, "hosts.bak");
        HostsArchiveList.TestArchiveDirectoryOverride = _tempDir; // ensure archives stored in temp
    }

    [TestCleanup]
    public void Cleanup()
    {
        HostsFile.TestBackupHostFilePathOverride = null;
        HostsArchiveList.TestArchiveDirectoryOverride = null;
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private HostsFile Create() => (HostsFile)typeof(HostsFile)
        .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(string)], null)!
        .Invoke([_tempFile]);

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
        File.WriteAllLines(_tempFile, ["127.0.0.9 changed"]);
        hf.Refresh();
        hf.Entries.First().IpAddress.ShouldBe("127.0.0.9");
    }

    // Guard for #99: disabling must not silently overwrite a DIFFERENT existing hosts.disabled.
    [TestMethod]
    public void DisableWouldOverwriteDifferentFile_NoDisabledFile_False()
    {
        var live = Path.Combine(_tempDir, "hosts");
        var disabled = Path.Combine(_tempDir, "hosts.disabled");
        File.WriteAllText(live, "a");
        HostsFile.DisableWouldOverwriteDifferentFile(live, disabled).ShouldBeFalse();
    }

    [TestMethod]
    public void DisableWouldOverwriteDifferentFile_IdenticalContent_False()
    {
        var live = Path.Combine(_tempDir, "hosts");
        var disabled = Path.Combine(_tempDir, "hosts.disabled");
        File.WriteAllText(live, "127.0.0.1 localhost\n10.0.0.1 a");
        File.WriteAllText(disabled, "127.0.0.1 localhost\n10.0.0.1 a");
        HostsFile.DisableWouldOverwriteDifferentFile(live, disabled).ShouldBeFalse();
    }

    [TestMethod]
    public void DisableWouldOverwriteDifferentFile_DifferentContent_True()
    {
        var live = Path.Combine(_tempDir, "hosts");
        var disabled = Path.Combine(_tempDir, "hosts.disabled");
        File.WriteAllText(live, "127.0.0.1 localhost");                 // fresh, foreign file
        File.WriteAllText(disabled, "10.0.0.1 curated\n10.0.0.2 more"); // the copy we must not lose
        HostsFile.DisableWouldOverwriteDifferentFile(live, disabled).ShouldBeTrue();
    }

    [TestMethod]
    public void DisableWouldOverwriteDifferentFile_SameLengthDifferentBytes_True()
    {
        var live = Path.Combine(_tempDir, "hosts");
        var disabled = Path.Combine(_tempDir, "hosts.disabled");
        File.WriteAllText(live, "127.0.0.1 aaaaa");
        File.WriteAllText(disabled, "127.0.0.1 bbbbb"); // same length, different content
        HostsFile.DisableWouldOverwriteDifferentFile(live, disabled).ShouldBeTrue();
    }

    // Buffer-boundary coverage for the chunked FilesHaveSameContent compare (4096-byte reads):
    // identical files at an exact multiple of the buffer, and two empty files, must both compare equal.
    [TestMethod]
    [DataRow(0)]      // both empty
    [DataRow(4096)]   // exactly one buffer
    [DataRow(8192)]   // exactly two buffers
    [DataRow(8193)]   // two buffers + a short trailing read
    public void DisableWouldOverwriteDifferentFile_IdenticalAtBufferBoundaries_False(int length)
    {
        var live = Path.Combine(_tempDir, "hosts");
        var disabled = Path.Combine(_tempDir, "hosts.disabled");
        var content = new byte[length];
        for (var i = 0; i < length; i++)
        {
            content[i] = (byte)(i % 251); // deterministic, spans byte values
        }

        File.WriteAllBytes(live, content);
        File.WriteAllBytes(disabled, content);
        HostsFile.DisableWouldOverwriteDifferentFile(live, disabled).ShouldBeFalse();
    }

    // The difference is in the LAST byte, past the first 4096-byte buffer — so a compare that only
    // checked the first chunk (and then returned "equal") would wrongly report no conflict and let the
    // large differing hosts.disabled be overwritten. This exercises the multi-buffer mismatch path.
    [TestMethod]
    public void DisableWouldOverwriteDifferentFile_DifferInLastByteAcrossBuffers_True()
    {
        var live = Path.Combine(_tempDir, "hosts");
        var disabled = Path.Combine(_tempDir, "hosts.disabled");
        var content = new byte[8193]; // two full buffers + 1 byte
        for (var i = 0; i < content.Length; i++)
        {
            content[i] = (byte)(i % 251);
        }

        File.WriteAllBytes(live, content);
        content[^1] ^= 0xFF; // flip only the final byte
        File.WriteAllBytes(disabled, content);
        HostsFile.DisableWouldOverwriteDifferentFile(live, disabled).ShouldBeTrue();
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
    public void Save_WritesToCurrentFilePath()
    {
        var hf = Create();
        hf.Entries.Add(new HostsEntry("127.0.0.5 added"));

        hf.Save();

        File.ReadAllLines(_tempFile)
            .ShouldContain(line => line.Contains("127.0.0.5", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Import_ClearsUndoHistory()
    {
        var hf = Create();
        UndoManager.Instance.ClearHistory();
        hf.Entries.Add(new HostsEntry("127.0.0.7 willbeorphaned"));
        UndoManager.Instance.CanUndo.ShouldBeTrue();

        var importFile = Path.Combine(_tempDir, "import.txt");
        File.WriteAllLines(importFile, ["127.0.0.2 other"]);
        hf.Import(importFile);

        // Undo actions from before the import reference replaced entries / stale indices;
        // Import must drop them so a later Undo can't crash.
        UndoManager.Instance.CanUndo.ShouldBeFalse();
    }

    [TestMethod]
    public void Import_FailedRead_LeavesEntriesIntact()
    {
        var hf = Create();
        var before = hf.Entries.Select(e => e.IpAddress).ToList();
        var missing = Path.Combine(_tempDir, "does-not-exist.txt");

        Should.Throw<Exception>(() => hf.Import(missing));

        // Read-before-clear: a failed import must not leave the model half-cleared.
        hf.Entries.Select(e => e.IpAddress).ShouldBe(before);
    }

    [TestMethod]
    public void Archive_CreatesArchiveEntry()
    {
        var hf = Create();
        var archiveName = Guid.NewGuid().ToString() + ".txt";
        hf.Archive(archiveName);
        File.Exists(Path.Combine(_tempDir, archiveName)).ShouldBeTrue();
        HostsArchiveList.Instance.Any(a => a.FilePath.EndsWith(archiveName, StringComparison.Ordinal)).ShouldBeTrue();
    }
}
