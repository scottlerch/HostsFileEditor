using HostsFileEditor.Elevation;

namespace HostsFileEditor.CommandLine;

/// <summary>
/// The verbs the headless command line understands (issue #2). <c>Apply</c> switches the live hosts
/// file to a saved preset (archive); the rest map to the corresponding <see cref="HostsFile"/> ops.
/// </summary>
public enum CliVerb
{
    Help,
    List,
    Apply,
    Enable,
    Disable,
    Import,
    Merge,
}

/// <summary>A parsed command: a <see cref="CliVerb"/> plus its single argument (a preset name or file path), if any.</summary>
public sealed record CliCommand(CliVerb Verb, string? Argument);

/// <summary>
/// The shared, headless command-line surface both editions run before showing any window (issue #2).
/// It parses argv into a <see cref="CliCommand"/> and executes it against the existing
/// <see cref="HostsFile"/> / <see cref="HostsArchiveList"/> APIs, writing plain text to the supplied
/// writers and returning a process exit code. Kept UI-free and dependency-free (a hand-rolled parser,
/// not a NuGet) so it is AOT/trim-safe for the modern edition and unit-testable in isolation.
/// </summary>
public static class HostsCli
{
    public const int ExitSuccess = 0;
    public const int ExitError = 1;   // a command ran but failed (bad preset/file, elevation declined, IO)
    public const int ExitUsage = 2;   // the arguments could not be parsed

    /// <summary>
    /// Parses and executes; writes results/errors to the writers; returns the process exit code. The
    /// caller decides <em>whether</em> to route to the CLI (any args that aren't a GUI-launch switch);
    /// an unrecognized command here is a user error and returns <see cref="ExitUsage"/> rather than
    /// silently opening a window.
    /// </summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(args, out var command, out var parseError))
        {
            error.WriteLine(parseError);
            output.WriteLine(UsageText);
            return ExitUsage;
        }

        try
        {
            return Execute(command, output, error);
        }
        catch (ElevationCancelledException)
        {
            error.WriteLine("Cancelled: administrator permission is required and was declined.");
            return ExitError;
        }
        catch (UnauthorizedAccessException)
        {
            // Direct write was denied and elevation wasn't available (e.g. hfe.exe run without its
            // Elevate\ helper alongside, or elevation blocked by policy).
            error.WriteLine("Access denied. Run this from an elevated console, or run the copy of hfe.exe installed with the app (its elevation helper must sit alongside it).");
            return ExitError;
        }
        // Top-level guard for a CLI: turn ANY other failure (a failed runas launch throws Win32Exception,
        // a corrupt hosts file, etc.) into a clean one-line message + non-zero exit, never a raw
        // stack-trace crash. CA1031 is intentional — this is the process's last-chance handler.
#pragma warning disable CA1031
        catch (Exception ex)
        {
            error.WriteLine($"Error: {ex.Message}");
            return ExitError;
        }
#pragma warning restore CA1031
    }

    /// <summary>Parses argv into a <see cref="CliCommand"/>. Returns false with a message on bad input.</summary>
    public static bool TryParse(IReadOnlyList<string> args, out CliCommand command, out string error)
    {
        ArgumentNullException.ThrowIfNull(args);
        command = new CliCommand(CliVerb.Help, null);
        error = string.Empty;

        if (args.Count == 0 || !TryMapVerb(args[0], out var verb))
        {
            error = $"Unknown command '{(args.Count == 0 ? string.Empty : args[0])}'.";
            return false;
        }

        var takesArgument = verb is CliVerb.Apply or CliVerb.Import or CliVerb.Merge;
        if (!takesArgument)
        {
            if (args.Count > 1)
            {
                error = $"'{args[0]}' takes no arguments.";
                return false;
            }

            command = new CliCommand(verb, null);
            return true;
        }

        // Apply / Import / Merge each require exactly one argument (preset name or file path).
        if (args.Count < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            var usageName = verb switch { CliVerb.Import => "import", CliVerb.Merge => "merge", _ => "apply" };
            error = verb == CliVerb.Apply
                ? "Missing preset name. Usage: apply <preset>"
                : $"Missing file path. Usage: {usageName} <file>";
            return false;
        }

        if (args.Count > 2)
        {
            error = $"Too many arguments for '{args[0]}'.";
            return false;
        }

        command = new CliCommand(verb, args[1]);
        return true;
    }

    private static int Execute(CliCommand command, TextWriter output, TextWriter error)
    {
        switch (command.Verb)
        {
            case CliVerb.Help:
                output.WriteLine(UsageText);
                return ExitSuccess;

            case CliVerb.List:
                return ListPresets(output);

            case CliVerb.Apply:
                return ApplyPreset(command.Argument!, output, error);

            case CliVerb.Enable:
                return EnableDisable(enable: true, output);

            case CliVerb.Disable:
                return EnableDisable(enable: false, output);

            case CliVerb.Import:
                return ImportFile(command.Argument!, output, error);

            case CliVerb.Merge:
                return MergeFile(command.Argument!, output, error);

            default:
                output.WriteLine(UsageText);
                return ExitSuccess;
        }
    }

    private static int ListPresets(TextWriter output)
    {
        var presets = HostsArchiveList.Instance
            .OrderBy(a => a, HostsArchive.FileNameComparer)
            .Select(a => a.FileName)
            .ToList();

        if (presets.Count == 0)
        {
            output.WriteLine("No presets found.");
            return ExitSuccess;
        }

        output.WriteLine("Presets:");
        foreach (var name in presets)
        {
            output.WriteLine($"  {name}");
        }

        return ExitSuccess;
    }

    private static int ApplyPreset(string name, TextWriter output, TextWriter error)
    {
        var archive = FindPreset(name);
        if (archive is null)
        {
            error.WriteLine($"Preset '{name}' not found. Run 'list' to see available presets.");
            return ExitError;
        }

        PrivilegedFileOperations.UseElevationHelper();
        HostsFile.Instance.Import(archive.FilePath);
        HostsFile.Instance.Save();
        output.WriteLine($"Applied preset '{archive.FileName}' to the hosts file.");
        NoteIfDisabled(output);
        return ExitSuccess;
    }

    // A mutating write goes to whatever file HostsFile currently targets — which is hosts.disabled when
    // the hosts file is disabled. Warn so "Applied/Imported/Merged ... to the hosts file" isn't read as
    // "the live file changed"; it takes effect on the next `enable`.
    private static void NoteIfDisabled(TextWriter output)
    {
        if (!HostsFile.IsEnabled)
        {
            output.WriteLine("Note: the hosts file is currently DISABLED, so this changed the disabled copy - run 'enable' for it to take effect.");
        }
    }

    private static int EnableDisable(bool enable, TextWriter output)
    {
        PrivilegedFileOperations.UseElevationHelper();

        if (enable)
        {
            if (HostsFile.IsEnabled)
            {
                output.WriteLine("The hosts file is already enabled.");
                return ExitSuccess;
            }

            HostsFile.Instance.EnableHostsFile();
            output.WriteLine("Enabled the hosts file.");
        }
        else
        {
            if (!HostsFile.IsEnabled)
            {
                output.WriteLine("The hosts file is already disabled.");
                return ExitSuccess;
            }

            HostsFile.Instance.DisableHostsFile();
            output.WriteLine("Disabled the hosts file.");
        }

        return ExitSuccess;
    }

    private static int ImportFile(string path, TextWriter output, TextWriter error)
    {
        if (!File.Exists(path))
        {
            error.WriteLine($"File not found: {path}");
            return ExitError;
        }

        PrivilegedFileOperations.UseElevationHelper();
        HostsFile.Instance.Import(path);
        HostsFile.Instance.Save();
        output.WriteLine($"Imported '{path}' and saved the hosts file.");
        NoteIfDisabled(output);
        return ExitSuccess;
    }

    private static int MergeFile(string path, TextWriter output, TextWriter error)
    {
        if (!File.Exists(path))
        {
            error.WriteLine($"File not found: {path}");
            return ExitError;
        }

        PrivilegedFileOperations.UseElevationHelper();
        var added = HostsFile.Instance.Merge(path);
        HostsFile.Instance.Save();
        output.WriteLine(added == 0
            ? "No new entries were added - every entry in that file is already present."
            : $"Merged {added} new {(added == 1 ? "entry" : "entries")} and saved the hosts file.");
        NoteIfDisabled(output);
        return ExitSuccess;
    }

    private static HostsArchive? FindPreset(string name)
    {
        // Tolerate surrounding whitespace from a quoted argument, then match by exact file name
        // (HostsArchiveList.FindByName) and finally ignoring the extension so `MyHosts1` resolves
        // `MyHosts1.txt` too.
        var trimmed = name.Trim();
        var presets = HostsArchiveList.Instance;
        return presets.FindByName(trimmed)
            ?? presets.FirstOrDefault(a => string.Equals(Path.GetFileNameWithoutExtension(a.FileName), trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryMapVerb(string token, out CliVerb verb)
    {
        // Normalize with ToUpperInvariant (CA1308) — matched against ASCII literals, case-insensitive.
        verb = token.ToUpperInvariant() switch
        {
            "HELP" or "--HELP" or "-H" or "-?" or "/?" => CliVerb.Help,
            "LIST" or "--LIST" or "-L" => CliVerb.List,
            "APPLY" or "SWITCH" or "-S" or "--SWITCH" => CliVerb.Apply,
            "ENABLE" or "--ENABLE" => CliVerb.Enable,
            "DISABLE" or "--DISABLE" => CliVerb.Disable,
            "IMPORT" or "--IMPORT" => CliVerb.Import,
            "MERGE" or "--MERGE" => CliVerb.Merge,
            _ => (CliVerb)(-1),
        };

        return Enum.IsDefined(verb);
    }

    public static string UsageText =>
        """
        Hosts File Editor - command line

        Usage:
          hfe <command> [argument]

        Commands:
          apply <preset>    Switch the hosts file to a saved preset (archive). Alias: -s <preset>
          enable            Enable the hosts file.
          disable           Disable the hosts file (renames it aside).
          import <file>     Replace the hosts file with <file>, then save.
          merge  <file>     Merge <file> into the hosts file (skipping duplicates), then save.
          list              List available presets.
          help              Show this help. Aliases: --help, -h

        Notes:
          Commands that change the hosts file need administrator rights; you will get a UAC
          prompt unless the shell is already elevated. With no command the app opens normally.
          Use hfe.exe in scripts (cmd waits for it); HostsFileEditor.exe also works but, being a
          windowed app, needs `start /wait` for sequential steps.

        Examples:
          hfe -s MyHosts1
          hfe disable
          hfe merge "C:\path\to\extra-hosts.txt"
        """;
}
