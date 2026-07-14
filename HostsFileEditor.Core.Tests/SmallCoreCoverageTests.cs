using HostsFileEditor.Utilities;
using HostsFileEditor.Win32;
using Microsoft.Win32;
using System.Reflection;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public sealed class SmallCoreCoverageTests
{
    [TestMethod]
    public void FileOpener_TryGetRegisteredApplication_ResolvesRegisteredHandler()
    {
        // Register a throwaway file association under HKCU\Software\Classes (no admin needed; visible
        // through the merged Registry.ClassesRoot view the resolver reads) so the success path — both
        // GetClassesRootKeyDefaultValue lookups plus the command cleanup — runs deterministically.
        const string ext = ".hfetest";
        const string progId = "hfetestfile";
        using (var classes = Registry.CurrentUser.CreateSubKey(@"Software\Classes"))
        {
            using var extKey = classes.CreateSubKey(ext);
            extKey.SetValue(null, progId);
            using var cmdKey = classes.CreateSubKey($@"{progId}\shell\open\command");
            cmdKey.SetValue(null, "\"C:\\tools\\editor.exe\" \"%1\"");
        }

        try
        {
            var method = typeof(FileOpener).GetMethod("TryGetRegisteredApplication", BindingFlags.NonPublic | BindingFlags.Static)!;
            var parameters = new object?[] { ext, null };

            var result = (bool)method.Invoke(null, parameters)!;

            result.ShouldBeTrue();
            var app = (string?)parameters[1];
            app.ShouldNotBeNullOrWhiteSpace();
            app!.ShouldContain("editor.exe");
        }
        finally
        {
            using var classes = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            classes?.DeleteSubKeyTree(ext, throwOnMissingSubKey: false);
            classes?.DeleteSubKeyTree(progId, throwOnMissingSubKey: false);
        }
    }

    [TestMethod]
    public void NativeMethods_IsRunningPackaged_ReturnsFalseForTestHost()
    {
        // The test host is a loose (unpackaged) process, so this must report false and never throw.
        NativeMethods.IsRunningPackaged().ShouldBeFalse();
    }

    [TestMethod]
    public void NativeMethods_RegisterWindowMessage_PlainString_ReturnsId()
    {
        var id = NativeMethods.RegisterWindowMessage("HFE_TEST_PLAIN_" + Guid.NewGuid().ToString("N"));
        id.ShouldBeGreaterThan(0);
    }
}
