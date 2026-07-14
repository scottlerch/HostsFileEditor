using HostsFileEditor.CommandLine;

namespace HostsFileEditor.Core.Tests;

[TestClass]
public class HostsCliTests
{
    [TestMethod]
    [DataRow("help", CliVerb.Help)]
    [DataRow("--help", CliVerb.Help)]
    [DataRow("-h", CliVerb.Help)]
    [DataRow("list", CliVerb.List)]
    [DataRow("enable", CliVerb.Enable)]
    [DataRow("DISABLE", CliVerb.Disable)]   // case-insensitive
    public void TryParse_NoArgVerbs(string token, CliVerb expected)
    {
        HostsCli.TryParse([token], out var command, out _).ShouldBeTrue();
        command.Verb.ShouldBe(expected);
        command.Argument.ShouldBeNull();
    }

    [TestMethod]
    [DataRow("apply", CliVerb.Apply)]
    [DataRow("-s", CliVerb.Apply)]          // issue #2's syntax
    [DataRow("switch", CliVerb.Apply)]
    [DataRow("import", CliVerb.Import)]
    [DataRow("merge", CliVerb.Merge)]
    public void TryParse_ArgVerbs_CaptureArgument(string token, CliVerb expected)
    {
        HostsCli.TryParse([token, "the-value"], out var command, out _).ShouldBeTrue();
        command.Verb.ShouldBe(expected);
        command.Argument.ShouldBe("the-value");
    }

    [TestMethod]
    public void TryParse_ArgVerb_MissingArgument_Fails()
    {
        HostsCli.TryParse(["apply"], out _, out var error).ShouldBeFalse();
        error.ShouldContain("preset");

        HostsCli.TryParse(["merge"], out _, out var error2).ShouldBeFalse();
        error2.ShouldContain("file");
    }

    [TestMethod]
    public void TryParse_NoArgVerb_WithExtraArg_Fails()
    {
        HostsCli.TryParse(["enable", "oops"], out _, out var error).ShouldBeFalse();
        error.ShouldContain("no arguments");
    }

    [TestMethod]
    public void TryParse_ArgVerb_WithTooManyArgs_Fails() =>
        HostsCli.TryParse(["import", "a", "b"], out _, out _).ShouldBeFalse();

    [TestMethod]
    public void TryParse_UnknownCommand_Fails()
    {
        HostsCli.TryParse(["frobnicate"], out _, out var error).ShouldBeFalse();
        error.ShouldContain("Unknown");
    }

    [TestMethod]
    public void Run_Help_WritesUsage_AndSucceeds()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = HostsCli.Run(["help"], output, error);

        code.ShouldBe(HostsCli.ExitSuccess);
        output.ToString().ShouldContain("apply <preset>");
    }

    [TestMethod]
    public void Run_UnknownCommand_WritesError_AndReturnsUsageExit()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = HostsCli.Run(["frobnicate"], output, error);

        code.ShouldBe(HostsCli.ExitUsage);
        error.ToString().ShouldContain("Unknown");
    }

    // #98: an extension-lenient stem that matches more than one archive must be reported as
    // ambiguous, not silently resolved by file-system enumeration order.
    [TestMethod]
    public void ResolvePreset_AmbiguousStem_ReportsAmbiguityWithAllNames()
    {
        WithTempArchives(
            dir =>
            {
                File.WriteAllText(Path.Combine(dir, "Foo.txt"), "a");
                File.WriteAllText(Path.Combine(dir, "Foo.bak"), "b");
            },
            () =>
            {
                var result = HostsCli.ResolvePreset("Foo", out var archive, out var names);
                result.ShouldBe(HostsCli.PresetResolution.Ambiguous);
                archive.ShouldBeNull();
                names.ShouldBe(["Foo.bak", "Foo.txt"]); // ordered by FileNameComparer, both listed
            });
    }

    [TestMethod]
    public void ResolvePreset_ExactNameBeatsStem()
    {
        WithTempArchives(
            dir =>
            {
                File.WriteAllText(Path.Combine(dir, "Foo"), "a");     // exact
                File.WriteAllText(Path.Combine(dir, "Foo.txt"), "b"); // stem match
            },
            () =>
            {
                HostsCli.ResolvePreset("Foo", out var archive, out _).ShouldBe(HostsCli.PresetResolution.Found);
                archive!.FileName.ShouldBe("Foo");
            });
    }

    [TestMethod]
    public void ResolvePreset_SingleStem_ResolvesWithExtension()
    {
        WithTempArchives(
            dir => File.WriteAllText(Path.Combine(dir, "Bar.txt"), "a"),
            () =>
            {
                HostsCli.ResolvePreset(" Bar ", out var archive, out _).ShouldBe(HostsCli.PresetResolution.Found);
                archive!.FileName.ShouldBe("Bar.txt");
            });
    }

    [TestMethod]
    public void ResolvePreset_NoMatch_NotFound()
    {
        WithTempArchives(
            dir => File.WriteAllText(Path.Combine(dir, "Bar.txt"), "a"),
            () => HostsCli.ResolvePreset("Nope", out _, out _).ShouldBe(HostsCli.PresetResolution.NotFound));
    }

    private static void WithTempArchives(Action<string> populate, Action body)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        HostsArchiveList.TestArchiveDirectoryOverride = dir;
        try
        {
            populate(dir);
            HostsArchiveList.Instance.Refresh();
            body();
        }
        finally
        {
            HostsArchiveList.TestArchiveDirectoryOverride = null;
            HostsArchiveList.Instance.Refresh();
            Directory.Delete(dir, true);
        }
    }
}
