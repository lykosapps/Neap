namespace Neap.Core.Settings;

/// <summary>Whether the headset's microphone is picking you up, and what is stopping it if not.</summary>
public enum MicState { Unknown, Live, Muted, ArmUp, MutedOnHeadset }

/// <summary>What a press of the app's microphone switch changes.</summary>
public enum MicChange { None, MuteSystem, UnmuteSystem, MuteHeadset, UnmuteHeadset }

/// <summary>What the headset reports of its microphones: the boom arm, the built-in microphones' mute, and which is in use.</summary>
/// <param name="Arm">The reported <c>mic_muted</c>, or null if the headset has not reported it.</param>
/// <param name="BuiltIn">The reported <c>builtin_mic_muted</c>, or null if the headset has not reported it.</param>
/// <param name="InUse">The reported <c>mic_in_use</c>, or null if the headset has not reported it.</param>
/// <remarks>
/// <para>
/// Each microphone has its own mute on the headset, and only the one in use
/// counts. With the boom microphone in, the arm is its mute, and the mute
/// button does nothing and reports nothing. With it out, the button mutes the
/// built-in microphones and the arm reads 0. The built-in microphones' mute
/// stays as it was while the boom is in, so it is ignored then; it is theirs
/// again the moment the boom comes out. Measured, with the boom unplugged
/// and plugged in while connected: <c>mic_in_use</c> is reported at once.
/// </para>
/// <para>
/// A headset that says nothing of which microphone is in use, such as the
/// Atlas Air, is taken to be using its boom, so its arm decides.
/// </para>
/// </remarks>
public readonly record struct HeadsetMic(int? Arm, int? BuiltIn, int? InUse)
{
    /// <summary>The <c>mic_in_use</c> reading while the built-in microphones are in use.</summary>
    public const int BuiltInInUse = 2;

    /// <summary>Gets whether the built-in microphones are the ones in use.</summary>
    public bool BuiltInUsed => InUse == BuiltInInUse;

    /// <summary>Gets the headset's own mute for the microphone in use, 1 while muted, or null if it is not known.</summary>
    /// <remarks>Anything but 0 or 1 is not a reading, so it is unknown rather than muted.</remarks>
    public int? Mute => (BuiltInUsed ? BuiltIn : Arm) is (0 or 1) and var mute ? mute : null;

    /// <summary>Gets whether a mute written to the headset mutes the microphone in use, so the app's switch can mute on the headset.</summary>
    /// <remarks>
    /// Only the built-in microphones' mute takes a write: the headset mutes,
    /// with its tone, as if the button were pressed. A write to the arm
    /// changes nothing, and with the boom in, the headset undoes a write to
    /// the built-in microphones' mute a moment later. Measured. With the
    /// boom, the switch mutes in the system instead.
    /// </remarks>
    public bool MutesOnHeadset => BuiltInUsed && BuiltIn is (0 or 1);
}

/// <summary>
/// The microphone as a person thinks of it: live, muted, or muted on the
/// headset by the boom arm or the mute button.
/// </summary>
/// <remarks>
/// <para>
/// The headset's <c>mic_muted</c> is the boom arm: 1 while it is flipped up.
/// Writing it changes nothing. Measured: with the arm down and 1 written, the
/// microphone went on picking up speech. So with the boom the microphone is
/// muted in Windows instead, which silences it for every application. A
/// Windows mute does not move the headset's values, so the two never get
/// confused. Which mute counts is <see cref="HeadsetMic"/>'s.
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

    /// <summary>The registry name of the headset's reading of its built-in microphones' mute.</summary>
    public const string BuiltInSetting = "builtin_mic_muted";

    /// <summary>The registry name of the headset's reading of which microphone is in use: 1 the boom, 2 the built-in ones.</summary>
    public const string InUseSetting = "mic_in_use";

    /// <param name="mic">What the headset reports of its microphones.</param>
    /// <param name="mutedInWindows">Whether Windows has the headset's microphone muted, or null if that cannot be read.</param>
    public static MicState Of(HeadsetMic mic, bool? mutedInWindows) => (mic.Mute, mic.BuiltInUsed, mutedInWindows) switch
    {
        (1, false, _) => MicState.ArmUp,
        (1, true, _) => MicState.MutedOnHeadset,
        (0, _, false) => MicState.Live,
        (0, _, true) => MicState.Muted,
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
    /// Only a move acts. A mute the person set from the app while the
    /// headset is unmuted stays until the headset's mute moves; so does one
    /// set while it is muted. On the first reading the headset muted mutes,
    /// and unmuted undoes only a mute the headset set: one from before Neap
    /// started, or before the headset was switched off, which the Atlas Air
    /// reports as muting as it goes.
    /// </para>
    /// </remarks>
    /// <param name="before">The <see cref="HeadsetMic.Mute"/> before, or null if there was none.</param>
    /// <param name="now">The <see cref="HeadsetMic.Mute"/> now, or null if it is not one.</param>
    /// <param name="mutedByHeadset">Whether the system's mute was last set by following the headset.</param>
    public static bool? SystemMuteFor(int? before, int? now, bool mutedByHeadset = false)
    {
        if (now is not (0 or 1) || before == now) return null;
        if (before is null) return now == 1 ? true : mutedByHeadset ? false : null;
        return now == 1;
    }

    /// <summary>What a press of the app's switch changes in this state.</summary>
    /// <param name="state">The microphone now.</param>
    /// <param name="headsetMutes">Whether the headset can mute the microphone in use, by <see cref="HeadsetMic.MutesOnHeadset"/>, and Neap may write it.</param>
    /// <remarks>
    /// Where the headset can mute itself the switch does that, so a press is
    /// the same as the headset's own button: the headset plays its tone, and
    /// the system follows it as it follows the button. A mute set in the
    /// system alone, from before or from another app, is undone there.
    /// </remarks>
    public static MicChange ChangeFor(MicState state, bool headsetMutes) => state switch
    {
        MicState.Live => headsetMutes ? MicChange.MuteHeadset : MicChange.MuteSystem,
        MicState.Muted => MicChange.UnmuteSystem,
        MicState.MutedOnHeadset when headsetMutes => MicChange.UnmuteHeadset,
        _ => MicChange.None,
    };

    /// <summary>Whether the app can mute or unmute the microphone in this state.</summary>
    /// <param name="state">The microphone now.</param>
    /// <param name="headsetMutes">Whether the headset can mute the microphone in use, by <see cref="HeadsetMic.MutesOnHeadset"/>, and Neap may write it.</param>
    public static bool CanChange(MicState state, bool headsetMutes) => ChangeFor(state, headsetMutes) != MicChange.None;
}
