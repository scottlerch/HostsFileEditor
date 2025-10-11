using HostsFileEditor.Utilities;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class FileExTests
{
    [TestMethod]
    public void DisableAttributes_RestoresAfterDispose()
    {
        var temp = Path.GetTempFileName();
        try
        {
            File.SetAttributes(temp, File.GetAttributes(temp) | FileAttributes.ReadOnly);
            var original = File.GetAttributes(temp);
            using (FileEx.DisableAttributes(temp, FileAttributes.ReadOnly))
            {
                File.GetAttributes(temp).HasFlag(FileAttributes.ReadOnly).ShouldBeFalse();
            }
            File.GetAttributes(temp).ShouldBe(original);
        }
        finally
        {
            File.SetAttributes(temp, FileAttributes.Normal);
            File.Delete(temp);
        }
    }
}
