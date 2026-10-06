using System.Runtime.InteropServices;
using Neap.Core.Audio.Pulse;

namespace Neap.Core.Audio;

/// <summary>Listens to the headset's microphone, for a level meter, for as long as it is open.</summary>
/// <remarks>
/// What is heard is reduced to the loudest sample since it was last asked;
/// the sound itself is not kept or sent anywhere.
/// </remarks>
public interface IMicrophoneListener : IDisposable
{
    /// <summary>Raised, on the recording thread, when recording stops on its own because of a fault.</summary>
    event Action<Exception>? Stopped;

    /// <summary>Takes the loudest level heard since the last take, from 0 to 1.</summary>
    float Take();
}

/// <summary>Opens the headset's microphone on whichever sound system this is.</summary>
public static class MicrophoneListening
{
    /// <summary>Starts listening to the headset's microphone.</summary>
    /// <exception cref="WindowsAudioException">The headset has no microphone the system can record from.</exception>
    /// <exception cref="PulseException">The sound server could not be asked, or has no microphone to record.</exception>
    /// <exception cref="PlatformNotSupportedException">This system has no sound system to listen to.</exception>
    public static IMicrophoneListener Open() =>
        OperatingSystem.IsWindows() ? MicrophoneListener.Open()
        : OperatingSystem.IsLinux() ? PulseListener.Open()
        : throw new PlatformNotSupportedException($"{Environment.OSVersion.Platform} has no microphone to listen to");

    /// <summary>Whether an exception is the sound system saying it cannot listen, rather than a fault of ours.</summary>
    public static bool IsRefusal(Exception ex) => ex is WindowsAudioException or PulseException or COMException;
}
