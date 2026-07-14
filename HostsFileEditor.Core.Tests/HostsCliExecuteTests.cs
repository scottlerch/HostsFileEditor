using HostsFileEditor.CommandLine;
using HostsFileEditor.Elevation;

namespace HostsFileEditor.Core.Tests;

/// <summary>
/// End-to-end coverage of <see cref="HostsCli.Run"/>'s execute paths and top-level error handling.
/// These run against the temp-file-bound <see cref="HostsFile.Instance"/> established by
/// <see cref="TestAssemblyInit"/>, so they exercise the real list/apply/enable/disable/import/merge
/// flow without touching the machine hosts file.
/// </summary>
[TestClass]
public sealed class HostsCliExecuteTests
{
    private string _archiveDir = null!;

    [TestInitialize]
    public void Init()
    {
        // A prior test may have swapped in a throwing elevation fake; restore the default first so the
        // re-enable below (which moves the file) can succeed.
        PrivilegedFileOperations.Current = new InProcessPrivilegedFileOperations();

        // A prior test may have left the file disabled (renamed aside). Put it back before reseeding.
        if (!HostsFile.IsEnabled)
        {
            HostsFile.Instance.EnableHostsFile();
        }

        File.WriteAllLines(HostsFile.DefaultHostFilePath, TestAssemblyInit.SeedLines);
        HostsFile.Instance.Refresh();

        _archiveDir = Path.Combine(Path.GetTempPath(), "HfeCliArchives_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_archiveDir);
        HostsArchiveList.TestArchiveDirectoryOverride = _archiveDir;
        HostsArchiveList.Instance.Refresh();
    }

    [TestCleanup]
    public void Cleanup()
    {
        HostsArchiveList.TestArchiveDirectoryOverride = null;
        HostsArchiveList.Instance.Refresh();
        PrivilegedFileOperations.Current = new InProcessPrivilegedFileOperations();

        // Leave the file enabled for the next test / class.
        if (!HostsFile.IsEnabled)
        {
            HostsFile.Instance.EnableHostsFile();
        }

        if (Directory.Exists(_archiveDir))
        {
            Directory.Delete(_archiveDir, true);
        }
    }

    private static (int code, string outText, string errText) RunCli(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = HostsCli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    [TestMethod]
    public void List_NoPresets_ReportsNone()
    {
        var (code, outText, _) = RunCli("list");

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("No presets found.");
    }

    [TestMethod]
    public void List_WithPresets_ListsThemSorted()
    {
        File.WriteAllText(Path.Combine(_archiveDir, "Zeta.txt"), "10.0.0.9 z");
        File.WriteAllText(Path.Combine(_archiveDir, "Alpha.txt"), "10.0.0.8 a");
        HostsArchiveList.Instance.Refresh();

        var (code, outText, _) = RunCli("list");

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("Presets:");
        outText.IndexOf("Alpha.txt", StringComparison.Ordinal)
            .ShouldBeLessThan(outText.IndexOf("Zeta.txt", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Apply_ExistingPreset_ReplacesHostsAndReports()
    {
        File.WriteAllLines(Path.Combine(_archiveDir, "Custom.txt"), ["192.168.1.1 router.test"]);
        HostsArchiveList.Instance.Refresh();

        var (code, outText, _) = RunCli("apply", "Custom.txt");

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("Applied preset 'Custom.txt'");
        File.ReadAllText(HostsFile.DefaultHostFilePath).ShouldContain("router.test");
        HostsFile.Instance.Entries.ShouldContain(e => e.HostNames == "router.test");
    }

    [TestMethod]
    public void Apply_StemResolvesPreset()
    {
        File.WriteAllLines(Path.Combine(_archiveDir, "MyHosts1.txt"), ["192.168.1.2 stem.test"]);
        HostsArchiveList.Instance.Refresh();

        var (code, _, _) = RunCli("-s", "MyHosts1");

        code.ShouldBe(HostsCli.ExitSuccess);
        File.ReadAllText(HostsFile.DefaultHostFilePath).ShouldContain("stem.test");
    }

    [TestMethod]
    public void Apply_MissingPreset_ReturnsError()
    {
        var (code, _, errText) = RunCli("apply", "does-not-exist");

        code.ShouldBe(HostsCli.ExitError);
        errText.ShouldContain("not found");
    }

    [TestMethod]
    public void Apply_AmbiguousStem_ReturnsError()
    {
        File.WriteAllText(Path.Combine(_archiveDir, "Dup.txt"), "a");
        File.WriteAllText(Path.Combine(_archiveDir, "Dup.bak"), "b");
        HostsArchiveList.Instance.Refresh();

        var (code, _, errText) = RunCli("apply", "Dup");

        code.ShouldBe(HostsCli.ExitError);
        errText.ShouldContain("ambiguous");
    }

    [TestMethod]
    public void Enable_AlreadyEnabled_ReportsNoOp()
    {
        var (code, outText, _) = RunCli("enable");

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("already enabled");
    }

    [TestMethod]
    public void Disable_ThenEnable_TogglesFile()
    {
        var (disableCode, disableOut, _) = RunCli("disable");
        disableCode.ShouldBe(HostsCli.ExitSuccess);
        disableOut.ShouldContain("Disabled the hosts file.");
        HostsFile.IsEnabled.ShouldBeFalse();

        // A second disable is a no-op.
        var (code2, out2, _) = RunCli("disable");
        code2.ShouldBe(HostsCli.ExitSuccess);
        out2.ShouldContain("already disabled");

        var (enableCode, enableOut, _) = RunCli("enable");
        enableCode.ShouldBe(HostsCli.ExitSuccess);
        enableOut.ShouldContain("Enabled the hosts file.");
        HostsFile.IsEnabled.ShouldBeTrue();
    }

    [TestMethod]
    public void Import_ExistingFile_ReplacesAndSaves()
    {
        var importFile = Path.Combine(_archiveDir, "import.txt");
        File.WriteAllLines(importFile, ["8.8.8.8 dns.test"]);

        var (code, outText, _) = RunCli("import", importFile);

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("Imported");
        File.ReadAllText(HostsFile.DefaultHostFilePath).ShouldContain("dns.test");
    }

    [TestMethod]
    public void Import_MissingFile_ReturnsError()
    {
        var (code, _, errText) = RunCli("import", Path.Combine(_archiveDir, "nope.txt"));

        code.ShouldBe(HostsCli.ExitError);
        errText.ShouldContain("File not found");
    }

    [TestMethod]
    public void Import_WhenDisabled_NotesDisabledState()
    {
        RunCli("disable");
        var importFile = Path.Combine(_archiveDir, "import.txt");
        File.WriteAllLines(importFile, ["8.8.4.4 dns2.test"]);

        var (code, outText, _) = RunCli("import", importFile);

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("currently DISABLED");
    }

    [TestMethod]
    public void Merge_AddsNewEntries()
    {
        var mergeFile = Path.Combine(_archiveDir, "merge.txt");
        File.WriteAllLines(mergeFile, ["4.4.4.4 brandnew.test"]);

        var (code, outText, _) = RunCli("merge", mergeFile);

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("Merged 1 new entry");
        File.ReadAllText(HostsFile.DefaultHostFilePath).ShouldContain("brandnew.test");
    }

    [TestMethod]
    public void Merge_AllDuplicates_ReportsNothingAdded()
    {
        var mergeFile = Path.Combine(_archiveDir, "merge.txt");
        // Same entries as the seed → nothing new to add.
        File.WriteAllLines(mergeFile, TestAssemblyInit.SeedLines);

        var (code, outText, _) = RunCli("merge", mergeFile);

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("No new entries");
    }

    [TestMethod]
    public void Merge_MultipleEntries_ReportsPluralCount()
    {
        var mergeFile = Path.Combine(_archiveDir, "merge.txt");
        File.WriteAllLines(mergeFile, ["4.4.4.4 one.test", "5.5.5.5 two.test"]);

        var (code, outText, _) = RunCli("merge", mergeFile);

        code.ShouldBe(HostsCli.ExitSuccess);
        outText.ShouldContain("Merged 2 new entries");
    }

    [TestMethod]
    public void Merge_MissingFile_ReturnsError()
    {
        var (code, _, errText) = RunCli("merge", Path.Combine(_archiveDir, "nope.txt"));

        code.ShouldBe(HostsCli.ExitError);
        errText.ShouldContain("File not found");
    }

    [TestMethod]
    public void Run_ElevationDeclined_ReportsCancelled()
    {
        PrivilegedFileOperations.Current = new ThrowingPrivilegedFileOperations(new ElevationCancelledException());
        var importFile = Path.Combine(_archiveDir, "import.txt");
        File.WriteAllLines(importFile, ["8.8.8.8 dns.test"]);

        var (code, _, errText) = RunCli("import", importFile);

        code.ShouldBe(HostsCli.ExitError);
        errText.ShouldContain("administrator permission is required");
    }

    [TestMethod]
    public void Run_AccessDenied_ReportsFriendlyMessage()
    {
        PrivilegedFileOperations.Current = new ThrowingPrivilegedFileOperations(new UnauthorizedAccessException());
        var importFile = Path.Combine(_archiveDir, "import.txt");
        File.WriteAllLines(importFile, ["8.8.8.8 dns.test"]);

        var (code, _, errText) = RunCli("import", importFile);

        code.ShouldBe(HostsCli.ExitError);
        errText.ShouldContain("Access denied");
    }

    [TestMethod]
    public void Run_UnexpectedError_ReportsGenericMessage()
    {
        PrivilegedFileOperations.Current = new ThrowingPrivilegedFileOperations(new InvalidOperationException("boom"));
        var importFile = Path.Combine(_archiveDir, "import.txt");
        File.WriteAllLines(importFile, ["8.8.8.8 dns.test"]);

        var (code, _, errText) = RunCli("import", importFile);

        code.ShouldBe(HostsCli.ExitError);
        errText.ShouldContain("Error: boom");
    }

    /// <summary>An elevation stub whose privileged ops always throw a chosen exception.</summary>
    private sealed class ThrowingPrivilegedFileOperations(Exception toThrow) : IPrivilegedFileOperations
    {
        public void WriteAllLines(string path, IEnumerable<string> lines) => throw toThrow;

        public void Move(string sourcePath, string destinationPath) => throw toThrow;
    }
}
