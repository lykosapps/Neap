namespace Neap.Core.Settings;

/// <summary>Whether the headset's microphone is picking you up, and what is stopping it if not.</summary>
public enum MicState { Unknown, Live, Muted, ArmUp }

/// <summary>
/// The microphone as a person thinks of it: live, muted, or muted by the
/// boom arm.
/// </summary>
/// <remarks>
/// <para>
/// The headset's <c>mic_muted</c> is the boom arm: 1 while it is flipped up.
/// Writing it changes nothing. Measured: with the arm down and 1 written, the
/// microphone went on picking up speech. So the microphone is muted in
/// Windows instead, which silences it for every application, and the
/// headset's value says only where the arm is. A Windows mute does not move
/// the headset's value, so the two never get confused.
/// </para>
/// <para>
/// While the arm is up nothing on the PC can make the microphone live, so
/// that is a state of its own, and the mute cannot be changed from the app
/// until the arm comes down.
/// </para>
/// </remarks>
public static class Microphone
{
    /// <summary>The registry name of the headset's reading of the boom arm.</summary>
    public const string ArmSetting = "mic_muted";

    /// <param name="arm">The reported <c>mic_muted</c>, or null if the headset has not reported it.</param>
    /// <param name="mutedInWindows">Whether Windows has the headset's microphone muted, or null if that cannot be read.</param>
    /// <remarks>
    /// Anything but 0 or 1 from the headset is not a reading of the arm, so
    /// it is unknown rather than muted.
    /// </remarks>
    public static MicState Of(int? arm, bool? mutedInWindows) => (arm, mutedInWindows) switch
    {
        (1, _) => MicState.ArmUp,
        (0, false) => MicState.Live,
        (0, true) => MicState.Muted,
        _ => MicState.Unknown,
    };

    /// <summary>What the system's mute should become because the headset's own mute moved, or null to leave it as it is.</summary>
    /// <remarks>
    /// <para>
    /// The boom arm and the mute button are the headset muting itself, before
    /// the sound reaches the PC, so a chat application on the PC hears the
    /// same silence as the person who muted it only if the system is muted too.
    /// Raising the arm mutes the system, and lowering it unmutes it, so the
    /// headset and the system never disagree about whether you can be heard.
    /// </para>
    /// <para>
    /// Only a move acts. A mute the person set from the app while the arm is
    /// down stays until the arm moves; so does one set while it is up. On the
    /// first reading a raised arm mutes, but a lowered one leaves the system
    /// alone: nothing has been muted by the headset yet.
    /// </para>
    /// </remarks>
    /// <param name="before">The reading before, or null if there was none.</param>
    /// <param name="now">The reading now, or null if it is not one.</param>
    public static bool? SystemMuteFor(int? before, int? now)
    {
        if (now is not (0 or 1) || before == now) return null;
        if (before is null) return now == 1 ? true : null;
        return now == 1;
    }

    /// <summary>Whether the app can mute or unmute the microphone in this state.</summary>
    public static bool CanChange(MicState state) => state is MicState.Live or MicState.Muted;
}
