using System.Diagnostics;
using System.Runtime.Versioning;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Neap.Core.Mix;

/// <summary>Windows' audio sessions, one per application per output.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsPlayback : IPlayback
{
    private static readonly Stopwatch Clock = Stopwatch.StartNew();

    /// <summary>Shared by every mix, so a program is looked up once however many times the mix restarts.</summary>
    private static readonly ProcessNames Names =
        new(Programs.NameOf, () => Clock.Elapsed, name => SessionMix.LookedUp?.Invoke(name));

    private readonly MMDeviceEnumerator _devices = new();

    /// <remarks>
    /// <para>
    /// Not the first endpoint whose name matches. Two transmitters plugged in
    /// at once give two endpoints that both answer to "Stealth Pro", the
    /// Charging Dock's and the USB Transmitter's, and the first match can
    /// leave the mix holding levels on one device while the person listens to
    /// the other, with nothing to show for it.
    /// </para>
    /// <para>
    /// The default output is the only endpoint the mix has any business
    /// touching, so this asks for that one and checks it is the headset,
    /// rather than finding a headset and then asking whether it is the default.
    /// </para>
    /// </remarks>
    public IPlaybackDevice? Headset()
    {
        try
        {
            var current = _devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (current.FriendlyName.Contains(Audio.AudioEndpoints.DefaultMatch, StringComparison.OrdinalIgnoreCase))
                return new Device(current);
            current.Dispose();
            return null;
        }
        catch { return null; }
    }

    public IEnumerable<IPlaybackDevice> Others(IPlaybackDevice headset)
    {
        foreach (var device in _devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            if (device.ID == headset.Id) device.Dispose();
            else yield return new Device(device);
        }
    }

    public IPlaybackDevice? Find(string id)
    {
        var device = _devices.GetDevice(id);
        if (device.State == DeviceState.Active) return new Device(device);
        device.Dispose();
        return null;
    }

    public void Dispose() => _devices.Dispose();

    private sealed class Device(MMDevice device) : IPlaybackDevice
    {
        public string Id => device.ID;

        public string Name => device.FriendlyName;

        public IReadOnlyList<IPlaybackSession> Sessions()
        {
            // NAudio hands back the same collection until asked again.
            device.AudioSessionManager.RefreshSessions();
            var sessions = device.AudioSessionManager.Sessions;
            var found = new List<IPlaybackSession>(sessions.Count);
            for (int i = 0; i < sessions.Count; i++)
                if (sessions[i].State != AudioSessionState.AudioSessionStateExpired)
                    found.Add(new Session(sessions[i]));
            return found;
        }

        public void Dispose() => device.Dispose();
    }

    private sealed class Session(AudioSessionControl session) : IPlaybackSession
    {
        public string? Id => session.GetSessionIdentifier;

        public string Program => Names.Of(session.GetProcessID);

        public bool Ours => session.GetProcessID == Environment.ProcessId;

        public bool Playing => session.State == AudioSessionState.AudioSessionStateActive;

        public float Volume
        {
            get => session.SimpleAudioVolume.Volume;
            set => session.SimpleAudioVolume.Volume = value;
        }
    }
}
