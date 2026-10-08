using System.Diagnostics;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using Neap.Core.Audio.Pulse;
using Neap.Core.Mix;

namespace Neap.Core.Audio;

/// <summary>Which programs are using a microphone, on whichever sound system this is.</summary>
/// <remarks>
/// <para>
/// Any microphone, not only the headset's: a call on another one is still a
/// call. A program counts while it is recording, which is when the system
/// shows its own microphone mark, not while it only holds a microphone open.
/// </para>
/// <para>
/// On Windows, a program with sound recording open has a session on that
/// microphone, as one playing has on an output, and Windows says which
/// sessions are active. Asking needs no rights a program does not already
/// have.
/// </para>
/// </remarks>
public static class MicrophoneUsers
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();
    private static readonly ProcessNames Names = new(Programs.NameOf, () => Clock.Elapsed);
    private static PulseClient? _pulse;

    /// <summary>Every program recording from a microphone now, by process name.</summary>
    /// <exception cref="PulseException">The sound server could not be asked.</exception>
    /// <exception cref="PlatformNotSupportedException">This system has no sound system to ask.</exception>
    public static IReadOnlyCollection<string> Now() =>
        OperatingSystem.IsWindows() ? OnWindows()
        : OperatingSystem.IsLinux() ? OnLinux()
        : throw new PlatformNotSupportedException($"{Environment.OSVersion.Platform} has no microphones to ask about");

    [SupportedOSPlatform("windows")]
    private static HashSet<string> OnWindows()
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var devices = new MMDeviceEnumerator();
        foreach (var device in devices.EnumerateAudioEndPoints(NAudio.CoreAudioApi.DataFlow.Capture, DeviceState.Active))
        {
            using (device)
            {
                var sessions = device.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++)
                    if (sessions[i].State == AudioSessionState.AudioSessionStateActive)
                        found.Add(Names.Of(sessions[i].GetProcessID));
            }
        }
        found.Remove("");
        return found;
    }

    /// <remarks>One connection to the sound server, kept from one look to the next.</remarks>
    [SupportedOSPlatform("linux")]
    private static HashSet<string> OnLinux()
    {
        _pulse ??= new PulseClient();
        return _pulse.Recording().Where(program => program.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
