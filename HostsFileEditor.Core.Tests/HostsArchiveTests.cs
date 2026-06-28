namespace HostsFileEditor.Core.Tests;

[TestClass]
public class HostsArchiveTests
{
    [TestMethod]
    public void FileName_ReturnsLastSegment()
    {
        var archive = new HostsArchive
        {
            FilePath = Path.Combine("a", "b", "c.txt")
        };
        archive.FileName.ShouldBe("c.txt");
    }

    [TestMethod]
    public void Validate_NonExistingFileName_IsValid()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        HostsArchive.Validate(path, out var error).ShouldBeTrue();
        error.ShouldBeEmpty();
    }

    [TestMethod]
    public void Validate_ExistingNameInArchive_ReturnsArchiveExists()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            HostsArchiveList.TestArchiveDirectoryOverride = dir;

            const string fileName = "dup.txt";
            File.WriteAllText(Path.Combine(dir, fileName), "x");

            // A name that already exists in the (effective) archive directory is rejected,
            // case-insensitively, regardless of whether a bare name or full path is passed.
            HostsArchive.Validate(fileName, out var error).ShouldBeFalse();
            error.ShouldNotBeNullOrEmpty();

            HostsArchive.Validate("DUP.TXT", out var errorUpper).ShouldBeFalse();
            errorUpper.ShouldNotBeNullOrEmpty();

            HostsArchive.Validate(Path.Combine(dir, fileName), out var errorFull).ShouldBeFalse();
            errorFull.ShouldNotBeNullOrEmpty();

            // A different name is accepted.
            HostsArchive.Validate("brandnew.txt", out var errorNew).ShouldBeTrue();
            errorNew.ShouldBeEmpty();
        }
        finally
        {
            HostsArchiveList.TestArchiveDirectoryOverride = null;
            Directory.Delete(dir, true);
        }
    }
}
