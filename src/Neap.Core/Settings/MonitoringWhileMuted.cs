namespace Neap.Core.Settings;

/// <summary>
/// Mic monitoring through a mute the person sets from the app, on a headset
/// whose own monitoring would otherwise go on while the microphone is muted.
/// </summary>
/// <remarks>
/// <para>
/// The app mutes the microphone in the system, which the headset never hears
/// of, so the headset goes on playing the person's voice back to them. The
/// Atlas Air's own mute silences its monitoring with the microphone (SP2Tools
/// F120), so the app's mute does the same: monitoring goes down to nothing
/// while muted, and back to where it was on unmuting.
/// </para>
/// <para>
/// A level the person sets while muted is theirs and is left alone, and so
/// is a level that was already nothing.
/// </para>
/// </remarks>
public static class MonitoringWhileMuted
{
    /// <summary>The monitoring level to set as the person mutes or unmutes, or null to leave it as it is.</summary>
    /// <param name="muted">Whether the person is muting.</param>
    /// <param name="now">The monitoring level the headset reports now, or null if it has not.</param>
    /// <param name="before">The level kept from the last mute, or null if none was.</param>
    public static int? LevelFor(bool muted, int? now, int? before) => muted
        ? now is > 0 ? 0 : null
        : now == 0 && before is > 0 ? before : null;

    /// <summary>The level to keep for unmuting, as the person mutes.</summary>
    public static int? KeptFor(int? now) => now is > 0 ? now : null;
}
