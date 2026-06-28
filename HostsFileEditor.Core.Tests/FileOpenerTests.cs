using HostsFileEditor.Utilities;
using System.Reflection;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class FileOpenerTests
{
    [TestMethod]
    public void TryGetRegisteredApplication_ReturnsFalseForUnknown()
    {
        var method = typeof(FileOpener).GetMethod("TryGetRegisteredApplication", BindingFlags.NonPublic | BindingFlags.Static);
        var parameters = new object?[] { ".unlikelyext", null };
        var result = (bool)method!.Invoke(null, parameters)!;
        result.ShouldBeFalse();
    }

    [TestMethod]
    public void GetClassesRootKeyDefaultValue_NullForInvalid()
    {
        var method = typeof(FileOpener).GetMethod("GetClassesRootKeyDefaultValue", BindingFlags.NonPublic | BindingFlags.Static);
        var value = method!.Invoke(null, [".unlikelyext\\doesnotexist"]);
        value.ShouldBeNull();
    }
}
