using HostsFileEditor.Elevation;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public sealed class PrivilegedFileOperationsTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Init()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (!Directory.Exists(_tempDir))
        {
            return;
        }

        // Clear any read-only attributes a test left behind so the recursive delete succeeds.
        foreach (var file in Directory.GetFiles(_tempDir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void InProcess_WriteAllLines_WritesContent()
    {
        var ops = new InProcessPrivilegedFileOperations();
        var path = Path.Combine(_tempDir, "hosts");

        ops.WriteAllLines(path, ["127.0.0.1 a", "127.0.0.2 b"]);

        File.ReadAllLines(path).ShouldBe(["127.0.0.1 a", "127.0.0.2 b"]);
    }

    [TestMethod]
    public void InProcess_Move_RenamesFile()
    {
        var ops = new InProcessPrivilegedFileOperations();
        var source = Path.Combine(_tempDir, "hosts");
        var destination = Path.Combine(_tempDir, "hosts.disabled");
        File.WriteAllText(source, "x");

        ops.Move(source, destination);

        File.Exists(source).ShouldBeFalse();
        File.Exists(destination).ShouldBeTrue();
    }

    [TestMethod]
    public void InProcess_WriteAllLines_OverwritesReadOnlyTarget()
    {
        var ops = new InProcessPrivilegedFileOperations();
        var path = Path.Combine(_tempDir, "hosts");
        File.WriteAllText(path, "old");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        ops.WriteAllLines(path, ["new"]);

        File.ReadAllLines(path).ShouldBe(["new"]);
    }

    [TestMethod]
    public void ElevatedHelper_WritableTarget_WritesDirectlyWithoutInvokingHelper()
    {
        // The helper path is intentionally bogus. Because the target is user-writable, the
        // direct write succeeds and the helper is never launched (no elevation prompt) — if
        // it tried to launch the missing helper this would throw.
        var ops = new ElevatedHelperPrivilegedFileOperations(Path.Combine(_tempDir, "does-not-exist.exe"));
        var path = Path.Combine(_tempDir, "out.txt");

        ops.WriteAllLines(path, ["a", "b"]);

        File.ReadAllLines(path).ShouldBe(["a", "b"]);
    }

    [TestMethod]
    public void ElevatedHelper_WritableTarget_MovesDirectlyWithoutInvokingHelper()
    {
        var ops = new ElevatedHelperPrivilegedFileOperations(Path.Combine(_tempDir, "does-not-exist.exe"));
        var source = Path.Combine(_tempDir, "a.txt");
        var destination = Path.Combine(_tempDir, "b.txt");
        File.WriteAllText(source, "x");

        ops.Move(source, destination);

        File.Exists(destination).ShouldBeTrue();
    }
}
