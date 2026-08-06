using System;
using System.Threading.Tasks;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

/// <summary>
/// Drives the optional update flow for the GUI: check, then (if a newer build exists) offer it and,
/// on confirmation, download and apply. Updates are never forced and never gate functionality.
/// </summary>
public interface IUpdateCoordinator
{
    /// <summary>
    /// Checks for an update and prompts to apply it if one exists. When <paramref name="notifyWhenCurrent"/>
    /// is true, also reports "up to date" / failures (for a user-invoked check); false stays silent
    /// (for the quiet startup check).
    /// </summary>
    Task CheckAsync(bool notifyWhenCurrent);
}

public sealed class UpdateCoordinator(
    IUpdateService updateService,
    IUpdateApplyService applyService,
    IDialogService dialog,
    ILogService log) : IUpdateCoordinator
{
    public async Task CheckAsync(bool notifyWhenCurrent)
    {
        var update = await updateService.CheckForUpdateAsync();

        if (!update.IsUpdateAvailable)
        {
            if (notifyWhenCurrent)
            {
                await dialog.AlertAsync("Up to date",
                    $"You are on the latest version (v{update.CurrentVersion}).");
            }
            return;
        }

        var confirmed = await dialog.ConfirmAsync("Update available",
            BuildAvailableMessage(update), "Update now", "Later");
        if (!confirmed) return;

        var result = await applyService.DownloadAndApplyAsync(update);
        switch (result)
        {
            case UpdateApplyResult.RestartTriggered:
                // The process is exiting into the swap; nothing more to show.
                break;
            case UpdateApplyResult.NotSingleFile:
                await dialog.AlertAsync("Update",
                    $"This build cannot update itself. Download the latest manually from\n{update.ReleasesPageUrl}");
                break;
            default:
                log.Warn($"Update apply returned {result}.");
                await dialog.AlertAsync("Update failed",
                    $"The update could not be applied ({result}). You can download it from\n{update.ReleasesPageUrl}");
                break;
        }
    }

    // Pure so it can be unit-tested without standing up dialogs. Release notes are shown as-is (trimmed
    // and capped) since they are authored on the release itself.
    public static string BuildAvailableMessage(UpdateCheckResult update)
    {
        const int maxNotesLength = 500;
        const string action = "Update now to get the latest version. You can also skip and keep using the current one.";

        var header = $"Version v{update.LatestVersion} is available (you have v{update.CurrentVersion}).";
        var notes = update.ReleaseNotes?.Trim();
        if (string.IsNullOrEmpty(notes)) return $"{header}\n\n{action}";

        var shown = notes.Length > maxNotesLength ? notes[..maxNotesLength].TrimEnd() + "..." : notes;
        return $"{header}\n\nWhat's new:\n{shown}\n\n{action}";
    }
}
