namespace StealthPro.Core.Connection;

/// <summary>What a section of headset controls shows for the headset's state.</summary>
public enum Fold
{
    /// <summary>Leave the section as it is: the state lasts a few seconds.</summary>
    Unchanged,
    /// <summary>Show the controls.</summary>
    Open,
    /// <summary>Hide the controls with nothing in their place: the page says it once for every section.</summary>
    Hidden,
    /// <summary>Hide the controls behind a line saying the headset is off or out of range.</summary>
    Off,
    /// <summary>Hide the controls behind a line saying the headset's settings are out of reach.</summary>
    Unreachable,
}

/// <summary>
/// Decides whether a section of headset controls folds away, and what stands
/// in its place.
/// </summary>
/// <remarks>
/// <para>
/// Greyed rows with dashes read as broken, so a section that cannot be used
/// is folded instead. Nothing plugged in is said once, at the top of the
/// page, so a section then folds with nothing of its own.
/// </para>
/// <para>
/// Connecting leaves every section as it was. It lasts a few seconds, and
/// folding for it would make the page jump twice.
/// </para>
/// </remarks>
public static class SectionFold
{
    /// <param name="status">The headset's state.</param>
    /// <param name="whenOff">
    /// The section goes on working without the headset's settings, as the mix
    /// and Windows' own volume do, so it folds only when the headset is away.
    /// </param>
    public static Fold For(HeadsetStatus status, bool whenOff) =>
        status.Link == Link.Connecting ? Fold.Unchanged
        : status.Link == Link.Absent ? Fold.Hidden
        : status.NotConnected ? Fold.Off
        : status.SettingsUnreachable && !whenOff ? Fold.Unreachable
        : Fold.Open;
}
