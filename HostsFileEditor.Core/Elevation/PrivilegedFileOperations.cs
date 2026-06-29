// Copyright 2025 Scott M. Lerch
// Licensed under GPL-2.0-or-later

namespace HostsFileEditor.Elevation;

/// <summary>
/// Process-wide selection of the <see cref="IPrivilegedFileOperations"/> implementation
/// used for hosts-file writes and enable/disable. Defaults to in-process; applications
/// call <see cref="UseElevationHelper"/> at startup to opt into on-demand elevation.
/// </summary>
public static class PrivilegedFileOperations
{
    /// <summary>File name of the elevation helper shipped alongside the application.</summary>
    public const string HelperExecutableName = "HostsFileEditor.Elevate.exe";

    /// <summary>
    /// Subdirectory (next to the app) the helper is deployed into. It lives in its own
    /// folder so its runtime resolution is not disturbed by the application's own
    /// (potentially self-contained) runtime files sitting beside the main executable.
    /// </summary>
    public const string HelperSubdirectory = "Elevate";

    /// <summary>
    /// The implementation used for privileged operations. Defaults to in-process so unit
    /// tests and already-elevated builds work without any configuration.
    /// </summary>
    public static IPrivilegedFileOperations Current { get; set; } = new InProcessPrivilegedFileOperations();

    /// <summary>
    /// Switches to on-demand elevation using the helper executable shipped next to the
    /// application. This is a no-op (stays in-process) when the helper cannot be found, so
    /// development and elevated builds keep working unchanged.
    /// </summary>
    /// <param name="helperPath">
    /// Optional explicit path to the helper; defaults to the helper next to the entry assembly.
    /// </param>
    public static void UseElevationHelper(string? helperPath = null)
    {
        if (helperPath is not null)
        {
            if (File.Exists(helperPath))
            {
                Current = new ElevatedHelperPrivilegedFileOperations(helperPath);
            }

            return;
        }

        // Prefer the helper in its dedicated subfolder; fall back to next to the app exe.
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, HelperSubdirectory, HelperExecutableName),
            Path.Combine(AppContext.BaseDirectory, HelperExecutableName),
        ];

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                Current = new ElevatedHelperPrivilegedFileOperations(candidate);
                return;
            }
        }
    }
}
