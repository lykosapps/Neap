namespace StealthPro.Core.Settings;

/// <summary>Whether the headset's microphone is picking you up.</summary>
public enum MicState { Unknown, Live, Muted }

/// <summary>
/// Reads and writes the microphone's mute (<c>mic_muted</c>) the way a person
/// thinks of it: a switch that is on while the microphone is live.
/// </summary>
/// <remarks>
/// The headset stores 1 for muted. A switch labelled "Muted" reads backwards
/// in the question people ask of it, "is my microphone on?": a live
/// microphone would show as a switch in the off position. So the switch is
/// the microphone, on when live, and this turns it into the headset's value.
/// </remarks>
public static class Microphone
{
    public const string Setting = "mic_muted";

    /// <param name="muted">The reported <c>mic_muted</c>, or null if the headset has not reported it.</param>
    public static MicState Of(int? muted) => muted switch
    {
        null => MicState.Unknown,
        0 => MicState.Live,
        _ => MicState.Muted,
    };

    /// <summary>The value that makes the microphone live, or mutes it.</summary>
    public static int ValueFor(bool live) => live ? 0 : 1;
}
