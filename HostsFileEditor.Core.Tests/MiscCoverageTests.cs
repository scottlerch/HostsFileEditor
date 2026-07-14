using HostsFileEditor.CommandLine;
using System.Reflection;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public sealed class MiscCoverageTests
{
    [TestMethod]
    public void ProgramSingleInstance_WmShowFirstInstance_IsRegistered()
    {
        // Touches the static initializer, which registers the window message via NativeMethods.
        ProgramSingleInstance.WmShowFirstInstance.ShouldBeGreaterThan(0);
    }

    [TestMethod]
    public void ConsoleAttach_AttachToParentConsole_DoesNotThrow()
    {
        // Under `dotnet test` stdout is redirected, so this takes the "already have a real stdout"
        // early-return path. Guard against the rare runner where stdout is NOT a real handle: there
        // the method attaches to the parent console and rebinds Console.Out/Error (a process-global,
        // irreversible side effect). Snapshot and restore them so this test can never corrupt another
        // test's output/logger capture. It must never throw regardless of console state.
        var savedOut = Console.Out;
        var savedError = Console.Error;
        try
        {
            Should.NotThrow(ConsoleAttach.AttachToParentConsole);
        }
        finally
        {
            Console.SetOut(savedOut);
            Console.SetError(savedError);
        }
    }

    [TestMethod]
    public void HostsArchiveList_MigrateLegacyArchives_IsSafeNoOp()
    {
        // With no test override, the private migration runs its real guard checks. It is best-effort
        // and must never throw; on a machine whose archive directory already exists (or has no legacy
        // directory) it returns immediately without side effects.
        var previousOverride = HostsArchiveList.TestArchiveDirectoryOverride;
        HostsArchiveList.TestArchiveDirectoryOverride = null;
        try
        {
            var method = typeof(HostsArchiveList).GetMethod(
                "MigrateLegacyArchives",
                BindingFlags.NonPublic | BindingFlags.Static)!;

            Should.NotThrow(() => method.Invoke(null, null));
        }
        finally
        {
            HostsArchiveList.TestArchiveDirectoryOverride = previousOverride;
        }
    }
}
