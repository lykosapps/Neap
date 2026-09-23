using System.Runtime.InteropServices;

namespace StealthPro.Core.Audio;

public class WindowsAudioException : Exception
{
    public WindowsAudioException(string message) : base(message) { }
}

public enum Flow { Output, Input }

public sealed record EndpointInfo(string Name, bool MatchedHeadset, int Percent, bool Muted);

/// <summary>
/// Windows output and input volume, via Core Audio.
///
/// Ported from stealthpro/winaudio.py. Two findings are baked in here and
/// are the reason this file exists at all:
///
/// The headset's own master-volume value (0x2a0) is a <b>mirror</b> of the
/// Windows output level, not a control: writing it changes the number the
/// headset reports, then Windows overwrites it again and nothing is heard.
/// The microphone is the same story one flow along — 0x610 mirrors the
/// Windows capture level. Set Windows and the headset follows.
///
/// Mute is what Swarm's "Audio Master Volume" toggle does. Turtle Beach's
/// own documentation says so, and two captures show the toggle sending
/// nothing at all to the headset. Worth knowing: Windows clears mute when it
/// sees a volume-up, and the headset's volume wheel sends native volume
/// keys, so turning the wheel while muted unmutes. That is Windows behaving
/// correctly, not the toggle failing.
/// </summary>
public static class AudioEndpoints
{
    /// <summary>
    /// The headset is the point of this app, so target its own endpoint
    /// rather than whatever Windows happens to be playing through. Without
    /// this, moving the slider while the default device is a pair of
    /// speakers would quietly change the speakers instead.
    /// </summary>
    public const string DefaultMatch = "stealth pro";

    public static int GetPercent(string match = DefaultMatch, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow);
        return (int)Math.Round(endpoint.GetLevel() * 100);
    }

    public static int SetPercent(int percent, string match = DefaultMatch, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow);
        endpoint.SetLevel(Math.Clamp(percent, 0, 100) / 100f);
        return (int)Math.Round(endpoint.GetLevel() * 100);
    }

    public static bool GetMuted(string match = DefaultMatch, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow);
        return endpoint.GetMute();
    }

    public static bool SetMuted(bool muted, string match = DefaultMatch, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow);
        endpoint.SetMute(muted);
        return endpoint.GetMute();
    }

    public static int GetMicPercent(string match = DefaultMatch) =>
        GetPercent(match, Flow.Input);

    public static int SetMicPercent(int percent, string match = DefaultMatch) =>
        SetPercent(percent, match, Flow.Input);

    public static EndpointInfo Describe(string match = DefaultMatch, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow);
        return new EndpointInfo(endpoint.Name, !endpoint.UsedDefault,
            (int)Math.Round(endpoint.GetLevel() * 100), endpoint.GetMute());
    }

    /// <summary>
    /// Current peak level on an endpoint, 0..1. Tells you whether audio is
    /// actually arriving at a device, as opposed to an application merely
    /// claiming to play to it.
    /// </summary>
    public static float Peak(string match = DefaultMatch, Flow flow = Flow.Output)
    {
        using var endpoint = Endpoint.Open(match, flow, fallBackToDefault: false);
        return endpoint.GetPeak();
    }

    /// <summary>Every active endpoint on a flow, by friendly name.</summary>
    public static List<string> ListNames(Flow flow = Flow.Output)
    {
        var names = new List<string>();
        var enumerator = Com.Create<IMMDeviceEnumerator>(Com.MMDeviceEnumeratorClsid);
        try
        {
            if (Com.Failed(enumerator.EnumAudioEndpoints(
                    flow == Flow.Output ? DataFlow.Render : DataFlow.Capture,
                    Com.DeviceStateActive, out var collection))) return names;
            try
            {
                collection.GetCount(out uint count);
                for (uint i = 0; i < count; i++)
                {
                    if (Com.Failed(collection.Item(i, out var device))) continue;
                    try { names.Add(Endpoint.FriendlyName(device)); }
                    finally { Marshal.ReleaseComObject(device); }
                }
            }
            finally { Marshal.ReleaseComObject(collection); }
        }
        finally { Marshal.ReleaseComObject(enumerator); }
        return names;
    }
}

/// <summary>One open endpoint, with its volume interface.</summary>
internal sealed class Endpoint : IDisposable
{
    private IMMDevice _device;
    private IAudioEndpointVolume? _volume;

    public string Name { get; private set; } = "";
    public bool UsedDefault { get; private set; }
    public string Id { get; private set; } = "";

    private Endpoint(IMMDevice device) => _device = device;

    public static Endpoint Open(string match, Flow flow, bool fallBackToDefault = true)
    {
        var dataFlow = flow == Flow.Output ? DataFlow.Render : DataFlow.Capture;
        var enumerator = Com.Create<IMMDeviceEnumerator>(Com.MMDeviceEnumeratorClsid);
        try
        {
            IMMDevice? device = string.IsNullOrEmpty(match)
                ? null : FindMatching(enumerator, match, dataFlow);
            bool usedDefault = false;

            if (device is null)
            {
                if (!fallBackToDefault)
                    throw new WindowsAudioException($"no endpoint matching '{match}'");
                // The headset is not present. Fall back to whatever Windows
                // is using, and say so rather than failing silently.
                usedDefault = true;
                if (Com.Failed(enumerator.GetDefaultAudioEndpoint(
                        dataFlow, Role.Multimedia, out device)) || device is null)
                    throw new WindowsAudioException("no audio device available");
            }

            var endpoint = new Endpoint(device) { UsedDefault = usedDefault };
            endpoint.Name = FriendlyName(device);
            device.GetId(out string id);
            endpoint.Id = id ?? "";
            return endpoint;
        }
        finally { Marshal.ReleaseComObject(enumerator); }
    }

    private IAudioEndpointVolume Volume
    {
        get
        {
            if (_volume is not null) return _volume;
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            if (Com.Failed(_device.Activate(ref iid, Com.ClsCtxAll, IntPtr.Zero, out object o)))
                throw new WindowsAudioException("could not open the volume interface");
            return _volume = (IAudioEndpointVolume)o;
        }
    }

    public float GetLevel()
    {
        int hr = Volume.GetMasterVolumeLevelScalar(out float level);
        if (Com.Failed(hr)) throw new WindowsAudioException($"read failed (0x{hr:x})");
        return level;
    }

    public void SetLevel(float level)
    {
        int hr = Volume.SetMasterVolumeLevelScalar(Math.Clamp(level, 0f, 1f), IntPtr.Zero);
        if (Com.Failed(hr)) throw new WindowsAudioException($"write failed (0x{hr:x})");
    }

    public bool GetMute()
    {
        int hr = Volume.GetMute(out bool muted);
        if (Com.Failed(hr)) throw new WindowsAudioException($"mute read failed (0x{hr:x})");
        return muted;
    }

    public void SetMute(bool muted)
    {
        int hr = Volume.SetMute(muted, IntPtr.Zero);
        if (Com.Failed(hr)) throw new WindowsAudioException($"mute write failed (0x{hr:x})");
    }

    public float GetPeak()
    {
        Guid iid = typeof(IAudioMeterInformation).GUID;
        if (Com.Failed(_device.Activate(ref iid, Com.ClsCtxAll, IntPtr.Zero, out object o)))
            throw new WindowsAudioException("no meter on that endpoint");
        var meter = (IAudioMeterInformation)o;
        try
        {
            if (Com.Failed(meter.GetPeakValue(out float peak)))
                throw new WindowsAudioException("meter read failed");
            return peak;
        }
        finally { Marshal.ReleaseComObject(meter); }
    }

    internal IMMDevice Device => _device;

    /// <summary>
    /// The headset's endpoint on a flow: the one Windows is using, if that is
    /// one of the headset's, and otherwise the first whose name matches.
    ///
    /// <b>The one in use first.</b> The headset can offer several endpoints at
    /// once — one per transmitter plugged in, and one more for itself over a
    /// USB-C cable — all answering to the same name. Taking the first meant
    /// the volume slider moved the Charging Dock's level while Windows played
    /// through the cable, which nobody could hear.
    /// </summary>
    internal static IMMDevice? FindMatching(IMMDeviceEnumerator enumerator, string match, DataFlow flow)
    {
        if (!Com.Failed(enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia, out var current))
            && current is not null)
        {
            if (FriendlyName(current).Contains(match, StringComparison.OrdinalIgnoreCase))
                return current;
            Marshal.ReleaseComObject(current);
        }

        if (Com.Failed(enumerator.EnumAudioEndpoints(flow, Com.DeviceStateActive, out var collection)))
            return null;
        try
        {
            collection.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                if (Com.Failed(collection.Item(i, out var device))) continue;
                if (FriendlyName(device).Contains(match, StringComparison.OrdinalIgnoreCase))
                    return device;
                Marshal.ReleaseComObject(device);
            }
        }
        finally { Marshal.ReleaseComObject(collection); }
        return null;
    }

    internal static string FriendlyName(IMMDevice device)
    {
        // PKEY_Device_FriendlyName
        var key = new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
        if (Com.Failed(device.OpenPropertyStore(Com.StgmRead, out var store))) return "";
        try
        {
            if (Com.Failed(store.GetValue(ref key, out var value))) return "";
            try
            {
                // A string lives at the front of the union, not where a blob's
                // data pointer does.
                return value.PointerValue == IntPtr.Zero
                    ? "" : Marshal.PtrToStringUni(value.PointerValue) ?? "";
            }
            finally { _ = Com.PropVariantClear(ref value); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    public void Dispose()
    {
        if (_volume is not null) { Marshal.ReleaseComObject(_volume); _volume = null; }
        if (_device is not null) Marshal.ReleaseComObject(_device);
        _device = null!;
    }
}
