using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Taskbar;

namespace HostsFileEditor;

/// <summary>
/// Populates the Windows taskbar Jump List with the saved archives/presets (issue #10), so the user
/// can right-click the taskbar icon and open a preset. Each entry relaunches the app with
/// <see cref="OpenArchiveSwitch"/> "&lt;path&gt;", which the startup path imports into the editor.
/// Best-effort: any failure is swallowed. See the PR notes for the packaged-identity / AOT caveats of
/// using the Win32 jump-list API from a packaged WinUI app.
/// </summary>
internal static class TaskbarJumpList
{
    /// <summary>Command-line switch a jump-list entry passes to open a preset in the editor.</summary>
    public const string OpenArchiveSwitch = "--open-archive";

    private const string PresetsCategory = "Presets";

    public static void Refresh()
    {
        if (!TaskbarManager.IsPlatformSupported)
        {
            return;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
        {
            return;
        }

        try
        {
            var jumpList = JumpList.CreateJumpList();

            var archives = HostsArchiveList.Instance
                .OrderBy(a => a.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (archives.Count > 0)
            {
                var category = new JumpListCustomCategory(PresetsCategory);
                foreach (var archive in archives)
                {
                    category.AddJumpListItems(new JumpListLink(exePath, archive.FileName)
                    {
                        Arguments = $"{OpenArchiveSwitch} \"{archive.FilePath}\"",
                        IconReference = new IconReference(exePath, 0),
                    });
                }

                jumpList.AddCustomCategories(category);
            }

            jumpList.Refresh();
        }
        catch (Exception)
        {
            // Jump list is a convenience; never let a shell/COM failure take down the app.
        }
    }

    /// <summary>
    /// Extracts the archive path from a <see cref="OpenArchiveSwitch"/> "&lt;path&gt;" argument pair,
    /// or <see langword="null"/> if not present.
    /// </summary>
    public static string? TryGetOpenArchivePath(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], OpenArchiveSwitch, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
