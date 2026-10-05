using Neap.Core.Updates;

namespace Neap.App.Services;

/// <summary>What the app says about where updating has got to, for everywhere that says it.</summary>
/// <remarks>
/// The Updates card in Settings and the banner share these sentences, so no
/// two places can word one differently. The sentences are in the resource
/// file.
/// </remarks>
public static class UpdateCopy
{
    /// <summary>The sentence for the stage, or null where there is nothing to say.</summary>
    public static string? Of(UpdateService updates)
    {
        string version = updates.Newer?.Version.ToString(3) ?? "";
        return updates.Stage switch
        {
            UpdateStage.Checking => Strings.Get("Settings_UpdateChecking"),
            UpdateStage.UpToDate => Strings.Format("Settings_UpdateUpToDate", AppInfo.Name),
            UpdateStage.CheckFailed => Strings.Get("Settings_UpdateCheckFailed"),
            UpdateStage.Available => Strings.Format("Settings_UpdateAvailable", version),
            UpdateStage.CannotUpdateHere => Strings.Format("Settings_UpdateCannotHere", version, AppInfo.Name),
            UpdateStage.Downloading => Strings.Format("Settings_UpdateDownloading", version, updates.Percent),
            UpdateStage.Installing => Strings.Format("Settings_UpdateInstalling", version),
            UpdateStage.UpdateFailed => Strings.Format("Settings_UpdateFailed", version),
            _ => null,
        };
    }
}
