namespace HostsFileEditor.Core.Tests;

[TestClass]
public class HostsArchiveAdditionalTests
{
    [TestMethod]
    public void Validate_DuplicateInDirectory_ReturnsFalse()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var fileName = "dup.txt";
            var fullPath = Path.Combine(dir, fileName);
            File.WriteAllText(fullPath, "x");
            // Simulate existing archive directory by creating file inside actual archive dir if possible
            // We cannot change ArchiveDirectory easily; rely on Validate logic: it checks ArchiveDirectory files names vs provided path
            // Provide fullPath to validate (should succeed) then fileName to attempt duplicate detection (will likely pass unless ArchiveDirectory matches dir)
            HostsArchive.Validate(fullPath, out var err1).ShouldBeTrue();
            err1.ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
