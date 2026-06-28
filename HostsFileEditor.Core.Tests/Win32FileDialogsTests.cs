using HostsFileEditor.Win32;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class Win32FileDialogsTests
{
    [TestMethod]
    public void BuildFilter_Empty_UsesAllFiles()
    {
        // Using reflection to access private method BuildFilter
        var method = typeof(Win32FileDialogs).GetMethod("BuildFilter", BindingFlags.NonPublic | BindingFlags.Static);
        var result = (string)method!.Invoke(null, [""])!;
        result.ShouldContain("All Files");
        result.ShouldEndWith("\0");
    }

    [TestMethod]
    public void BuildFilter_ParsesPairs()
    {
        var method = typeof(Win32FileDialogs).GetMethod("BuildFilter", BindingFlags.NonPublic | BindingFlags.Static);
        var filter = "Text Files|*.txt|Image Files|*.png";
        var result = (string)method!.Invoke(null, [filter])!;
        result.ShouldContain("Text Files");
        result.ShouldContain("*.png");
        result.ShouldEndWith("\0");
    }

    [TestMethod]
    public void ClearBuffer_WritesZeros()
    {
        var clearMethod = typeof(Win32FileDialogs).GetMethod("ClearBuffer", BindingFlags.NonPublic | BindingFlags.Static);
        var ptr = Marshal.AllocHGlobal(4);
        try
        {
            Marshal.WriteInt32(ptr, unchecked((int)0xFFFFFFFF));
            clearMethod!.Invoke(null, [ptr, 4]);
            var bytes = new byte[4];
            Marshal.Copy(ptr, bytes, 0, 4);
            bytes.ShouldAllBe(b => b == 0);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    [TestMethod]
    public void BuildFilter_SinglePairTrailingIgnored()
    {
        var method = typeof(Win32FileDialogs).GetMethod("BuildFilter", BindingFlags.NonPublic | BindingFlags.Static);
        var filter = "Text|*.txt|Dangling"; // odd count -> last part ignored
        var result = (string)method!.Invoke(null, [filter])!;
        result.ShouldContain("Text");
        result.ShouldContain("*.txt");
    }
}
