namespace Neap.Core.Connection;

/// <summary>What a section of controls shows for the headset's state.</summary>
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
/// Decides whether a section of controls folds away, and what stands in its
/// place.
/// </summary>
/// <remarks>
/// <para>
/// Greyed rows with dashes read as broken, so a section that cannot be used
/// is folded instead. Nothing plugged in is said once, at the top of the
/// page, so a section then folds with nothing of its own.
/// </para>
/// <para>
/// A section folds by what it needs. The headset's settings need the headset
/// answering. The system's volume, microphone level, spatial sound, audio
/// format and the mix need only the sound device the system has for the
/// headset, which a dock or transmitter keeps while the headset is off or out
/// of range, and which the system's own access rules do not guard.
/// </para>
/// <para>
/// Connecting leaves every section as it was. It lasts a few seconds, and
/// folding for it would make the page jump twice.
/// </para>
/// </remarks>
public static class SectionFold
{
    /// <param name="status">The headset's state.</param>
    /// <param name="soundOnly">
    /// The section needs only the system's sound device for the headset, not
    /// the headset's settings.
    /// </param>
    /// <param name="accessDenied">
    /// The system will not let this person use the headset's settings. A
    /// settings section folds with nothing in its place: the notice at the top
    /// of the page says what is wrong, and a line about the headset being off
    /// would contradict it.
    /// </param>
    public static Fold For(HeadsetStatus status, bool soundOnly, bool accessDenied = false) =>
        soundOnly ? ForSound(status, accessDenied) : ForSettings(status, accessDenied);

    private static Fold ForSettings(HeadsetStatus status, bool accessDenied) =>
        accessDenied ? Fold.Hidden
        : status.Link == Link.Connecting ? Fold.Unchanged
        : status.Link == Link.Absent ? Fold.Hidden
        : status.NotConnected ? Fold.Off
        : status.SettingsUnreachable ? Fold.Unreachable
        : Fold.Open;

    /// <remarks>
    /// Where a sound section cannot work, its card says why in the same words
    /// a settings section uses. Switched off on its cable, the headset may
    /// take its sound device with it, which has not been seen either way, so
    /// the section folds there until it has.
    /// </remarks>
    private static Fold ForSound(HeadsetStatus status, bool accessDenied) =>
        accessDenied ? Fold.Open
        : status.Link == Link.Connecting ? Fold.Unchanged
        : status.Link == Link.Absent ? Fold.Hidden
        : status.SwitchedOff ? Fold.Off
        : status.SettingsUnreachable && status.NoSound ? Fold.Unreachable
        : Fold.Open;
}
