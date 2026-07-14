namespace HostsFileEditor.Core.Tests;

/// <summary>
/// Assembly-wide setup that redirects the <see cref="HostsFile"/> singleton at a throwaway temp file
/// via the sanctioned <c>HFE_HOSTS_PATH</c> override, so tests can exercise the full headless-CLI and
/// enable/disable/save code paths (which go through <see cref="HostsFile.Instance"/>) without ever
/// touching the real system hosts file.
/// </summary>
/// <remarks>
/// <para>
/// The override is captured in a <c>static readonly</c> field the first time anything touches
/// <see cref="HostsFile"/>. <see cref="AssemblyInitialize"/> is the very first code MSTest runs, before
/// any test or its initializers, so setting the environment variable and creating the file here — then
/// forcing the singleton to construct — guarantees the singleton binds to our temp file.
/// </para>
/// <para>
/// No test in this assembly touched <see cref="HostsFile.Instance"/> before this harness existed, so
/// nothing races the capture. Instance-level <see cref="HostsFile"/> tests that build their own copy via
/// the private constructor pass an explicit path and are unaffected by the override.
/// </para>
/// </remarks>
[TestClass]
public static class TestAssemblyInit
{
    /// <summary>Directory holding the throwaway hosts file the singleton is bound to for this run.</summary>
    public static string HostsDirectory { get; private set; } = null!;

    /// <summary>The initial content written to the temp hosts file at assembly start.</summary>
    public static readonly string[] SeedLines =
    [
        "127.0.0.1 localhost",
        "10.0.0.1 example.test",
        "# a plain comment",
    ];

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        HostsDirectory = Path.Combine(Path.GetTempPath(), "HfeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(HostsDirectory);

        var hostsPath = Path.Combine(HostsDirectory, "hosts");
        File.WriteAllLines(hostsPath, SeedLines);

        // Bind the singleton to the temp file (env var is read once, on first HostsFile access below).
        Environment.SetEnvironmentVariable("HFE_HOSTS_PATH", hostsPath);
        HostsFile.TestBackupHostFilePathOverride = Path.Combine(HostsDirectory, "hosts.bak");

        // Force construction now, under known conditions, so the binding is deterministic.
        _ = HostsFile.Instance.Entries.Count;
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Environment.SetEnvironmentVariable("HFE_HOSTS_PATH", null);
        HostsFile.TestBackupHostFilePathOverride = null;

        try
        {
            if (Directory.Exists(HostsDirectory))
            {
                Directory.Delete(HostsDirectory, true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp directory; a leftover here never fails the run.
        }
    }
}
