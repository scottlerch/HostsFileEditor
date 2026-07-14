namespace HostsFileEditor.Core.Tests;

[TestClass]
public sealed class HostsArchiveGapTests
{
    [TestMethod]
    public void Validate_InvalidPath_ReturnsFalseWithMessage()
    {
        // An embedded null makes new FileInfo(...) throw, exercising Validate's catch path.
        HostsArchive.Validate("bad\0name", out var error).ShouldBeFalse();
        error.ShouldNotBeNullOrEmpty();
    }

    [TestMethod]
    public void Constructor_WithName_UsesEffectiveArchiveDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            HostsArchiveList.TestArchiveDirectoryOverride = dir;
            var archive = new HostsArchive("preset.txt");
            archive.FilePath.ShouldBe(Path.Combine(dir, "preset.txt"));
            archive.FileName.ShouldBe("preset.txt");
        }
        finally
        {
            HostsArchiveList.TestArchiveDirectoryOverride = null;
        }
    }

    [TestMethod]
    public void Constructor_NullName_Throws() =>
        Should.Throw<ArgumentNullException>(() => new HostsArchive(null!));

    [TestMethod]
    public void FilePath_NullAssignment_Throws() =>
        Should.Throw<ArgumentNullException>(() => new HostsArchive { FilePath = null! });

    [TestMethod]
    public void FileNameComparer_OrdersCaseInsensitively()
    {
        var a = new HostsArchive { FilePath = @"x\alpha.txt" };
        var b = new HostsArchive { FilePath = @"x\BETA.txt" };
        HostsArchive.FileNameComparer.Compare(a, b).ShouldBeLessThan(0);
        HostsArchive.FileNameComparer.Compare(b, a).ShouldBeGreaterThan(0);
    }
}
