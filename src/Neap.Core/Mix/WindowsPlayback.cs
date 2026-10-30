using System.Diagnostics;
using System.Runtime.InteropServices;
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

    /// <remarks>Which output it is, and why, is <see cref="HeadsetOutput"/>'s: the headset's outputs are those whose name matches.</remarks>
    public IPlaybackDevice? Headset()
    {
        var headsets = new List<SoundOutput>();
        foreach (var device in _devices.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                if (HeadsetModels.SoundMatches(device.FriendlyName, Audio.AudioEndpoints.DefaultMatch))
                    headsets.Add(new SoundOutput(device.ID, device.FriendlyName));
            }
        }

        var (id, found) = HeadsetOutput.Pick(headsets, DefaultId(Role.Multimedia), DefaultId(Role.Communications));
        HeadsetOutput.Note(found, headsets.FirstOrDefault(o => o.Id == id).Name);
        return id is null ? null : new Device(_devices.GetDevice(id));
    }

    /// <summary>The default output's id for a role, or null when there is none.</summary>
    private string? DefaultId(Role role)
    {
        try
        {
            using var device = _devices.GetDefaultAudioEndpoint(DataFlow.Render, role);
            return device.ID;
        }
        catch (COMException) { return null; }
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

        /// <remarks>The description is read from the program's file, not from the running program; see <see cref="Programs"/>.</remarks>
        public string Display
        {
            get
            {
                string name = Programs.Readable(Program);
                if (Programs.PathOf(session.GetProcessID) is not { } path) return name;
                try
                {
                    return FileVersionInfo.GetVersionInfo(path).FileDescription is { } described
                           && !string.IsNullOrWhiteSpace(described) ? described : name;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return name; }
            }
        }

        public bool Ours => session.GetProcessID == Environment.ProcessId;

        public bool Playing => session.State == AudioSessionState.AudioSessionStateActive;

        public float Peak
        {
            get
            {
                // A session that has just closed no longer has a meter.
                try { return session.AudioMeterInformation.MasterPeakValue; }
                catch (COMException) { return 0; }
            }
        }

        public float Volume
        {
            get => session.SimpleAudioVolume.Volume;
            set => session.SimpleAudioVolume.Volume = value;
        }
    }
}
