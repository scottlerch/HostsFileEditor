using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.UI.StartScreen;

namespace HostsFileEditor;

/// <summary>
/// Populates the Windows taskbar Jump List with the saved archives/presets (issue #10) using the
/// packaged-native WinRT <see cref="JumpList"/> API (AOT-compatible via CsWinRT, unlike the COM-based
/// WindowsAPICodePack the classic edition uses). Each entry carries an <see cref="OpenArchivePrefix"/>
/// argument; the app is re-activated with that argument (fresh launch OR redirected to the running
/// instance — see App.OnLaunched / OnInstanceActivated) and imports the preset into the editor.
/// <para>
/// <see cref="JumpList"/> requires package identity, so <see cref="JumpList.IsSupported"/> is false
/// for an unpackaged (dev) run and every call here no-ops — the jump list only appears for the
/// installed MSIX. Best-effort throughout: a WinRT failure never surfaces to the user.
/// </para>
/// </summary>
internal static class TaskbarJumpList
{
    /// <summary>Prefix on the launch argument a jump-list entry passes to open a preset.</summary>
    public const string OpenArchivePrefix = "open-archive:";

    private const string PresetsCategory = "Presets";

    public static async Task RefreshAsync()
    {
        if (!JumpList.IsSupported())
        {
            return;
        }

        try
        {
            var jumpList = await JumpList.LoadCurrentAsync();

            // We own the whole list (no recent/frequent system group), so rebuild it from scratch.
            jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
            jumpList.Items.Clear();

            var archives = HostsArchiveList.Instance
                .OrderBy(a => a.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var archive in archives)
            {
                var item = JumpListItem.CreateWithArguments($"{OpenArchivePrefix}{archive.FilePath}", archive.FileName);
                item.GroupName = PresetsCategory;

                // Show the app icon instead of the default blank-document glyph. A packaged
                // ms-appx:/// asset; the resource loader picks the right scale/target-size variant.
                item.Logo = new Uri("ms-appx:///Assets/Square44x44Logo.png");

                jumpList.Items.Add(item);
            }

            await jumpList.SaveAsync();
        }
        catch (Exception)
        {
            // Jump list is a convenience; never let a WinRT failure take down the app.
        }
    }

    /// <summary>
    /// Extracts the archive path from a jump-list activation, or <see langword="null"/> if the
    /// activation is not an open-preset launch. Handles both the fresh-launch and redirected-to-
    /// running-instance activations (both are <see cref="ExtendedActivationKind.Launch"/>).
    /// </summary>
    public static string? TryGetOpenArchivePath(AppActivationArguments? args)
    {
        if (args?.Kind == ExtendedActivationKind.Launch &&
            args.Data is ILaunchActivatedEventArgs launch)
        {
            return TryGetOpenArchivePath(launch.Arguments);
        }

        return null;
    }

    /// <summary>Extracts the archive path from a raw launch-argument string.</summary>
    public static string? TryGetOpenArchivePath(string? arguments) =>
        !string.IsNullOrEmpty(arguments) && arguments.StartsWith(OpenArchivePrefix, StringComparison.Ordinal)
            ? arguments[OpenArchivePrefix.Length..]
            : null;
}
