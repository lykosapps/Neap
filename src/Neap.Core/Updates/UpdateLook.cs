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

/// <summary>Which link to the release page the update card offers, if any.</summary>
public enum UpdateLink { None, Notes, Download }

/// <summary>What the update card offers at each stage, decided once.</summary>
/// <remarks>
/// The main button stays on screen while a check or an update runs, pressing
/// it doing nothing: taking it away would throw keyboard focus on to the next
/// card. Updating leads once there is something to update to. Where Neap
/// can't update itself, the release page is the way to the new version, so
/// the link offers the download instead of what's new.
/// </remarks>
/// <param name="Action">What the main button does.</param>
/// <param name="Busy">Whether a check or an update is running, so a press does nothing.</param>
/// <param name="Leads">Whether the main button is the one to press.</param>
/// <param name="Link">Which link to the release page is offered.</param>
public readonly record struct UpdateLook(UpdateAction Action, bool Busy, bool Leads, UpdateLink Link)
{
    public static UpdateLook Of(UpdateStage stage) => stage switch
    {
        UpdateStage.Checking => new(UpdateAction.Check, Busy: true, Leads: false, UpdateLink.None),
        UpdateStage.Available or UpdateStage.UpdateFailed =>
            new(UpdateAction.Update, Busy: false, Leads: true, UpdateLink.Notes),
        UpdateStage.Downloading or UpdateStage.Installing =>
            new(UpdateAction.Update, Busy: true, Leads: true, UpdateLink.Notes),
        UpdateStage.CannotUpdateHere => new(UpdateAction.Check, Busy: false, Leads: false, UpdateLink.Download),
        _ => new(UpdateAction.Check, Busy: false, Leads: false, UpdateLink.None),
    };
}
