using System.Runtime.InteropServices;
using Neap.Core.Audio.Pulse;

namespace Neap.Core.Audio;

/// <summary>Opens the test tone on the headset on whichever sound system this is.</summary>
public static class ToneOutput
{
    /// <summary>Starts a silent tone on the headset's output.</summary>
    /// <exception cref="WindowsAudioException">The headset has no output Windows can play to.</exception>
    /// <exception cref="PulseException">The sound server could not be asked, or has no headset output to play to.</exception>
    /// <exception cref="PlatformNotSupportedException">This system has no sound system to play to.</exception>
    public static IPlayingTone Open(double frequency) =>
        OperatingSystem.IsWindows() ? TestTone.Open(frequency)
        : OperatingSystem.IsLinux() ? PulseTone.Open(frequency)
        : throw new PlatformNotSupportedException($"{Environment.OSVersion.Platform} has no sound to play a tone to");

    /// <summary>Whether an exception is the sound system saying it cannot play, rather than a fault of ours.</summary>
    public static bool IsRefusal(Exception ex) => ex is WindowsAudioException or PulseException or COMException;
}
