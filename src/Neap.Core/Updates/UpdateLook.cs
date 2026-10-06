namespace Neap.Core.Updates;

/// <summary>Where updating Neap has got to.</summary>
public enum UpdateStage
{
    NotChecked,
    Checking,
    UpToDate,
    CheckFailed,
    Available,

    /// <summary>A newer version is out, but Neap's folder can't be written to, so it has to be downloaded by hand.</summary>
    CannotUpdateHere,

    Downloading,
    Installing,
    UpdateFailed,
}

/// <summary>What the update card's main button does.</summary>
public enum UpdateAction { Check, Update }

/// <summary>What the update card and the banner offer at each stage, decided once.</summary>
/// <remarks>
/// The main button stays on screen while a check or an update runs, pressing
/// it doing nothing: taking it away would throw keyboard focus on to the next
/// card. Updating leads once there is something to update to. What's new is
/// offered whenever there is a newer version to read about. Where Neap can't
/// update itself, the release page is the way to the new version, so the
/// download is offered beside it.
/// </remarks>
/// <param name="Action">What the main button does.</param>
/// <param name="Busy">Whether a check or an update is running, so a press does nothing.</param>
/// <param name="Leads">Whether the main button is the one to press.</param>
/// <param name="Notes">Whether what's new is offered.</param>
/// <param name="Download">Whether a link to download the new version by hand is offered.</param>
public readonly record struct UpdateLook(UpdateAction Action, bool Busy, bool Leads, bool Notes, bool Download)
{
    public static UpdateLook Of(UpdateStage stage) => stage switch
    {
        UpdateStage.Checking => new(UpdateAction.Check, Busy: true, Leads: false, Notes: false, Download: false),
        UpdateStage.Available or UpdateStage.UpdateFailed =>
            new(UpdateAction.Update, Busy: false, Leads: true, Notes: true, Download: false),
        UpdateStage.Downloading or UpdateStage.Installing =>
            new(UpdateAction.Update, Busy: true, Leads: true, Notes: true, Download: false),
        UpdateStage.CannotUpdateHere =>
            new(UpdateAction.Check, Busy: false, Leads: false, Notes: true, Download: true),
        _ => new(UpdateAction.Check, Busy: false, Leads: false, Notes: false, Download: false),
    };
}
