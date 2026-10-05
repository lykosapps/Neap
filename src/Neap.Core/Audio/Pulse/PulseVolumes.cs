using System.Runtime.Versioning;

namespace Neap.Core.Audio.Pulse;

/// <summary>
/// The headset's volume and mute on Linux, for its output and for its
/// microphone: <see cref="AudioEndpoints"/>' counterpart.
/// </summary>
/// <remarks>
/// <para>
/// The headset's device is the one whose USB ids say so, found before the
/// default is: moving the slider while the default is a pair of speakers
/// would otherwise change the speakers. With no headset device it falls back
/// to the default and says so through <see cref="EndpointInfo.MatchedHeadset"/>.
/// </para>
/// <para>
/// A volume is the number the desktop's own mixer shows, so what Neap shows
/// is what the person sees when they look there.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
public static class PulseVolumes
{
    /// <summary>The headset's device on a flow: how loud and whether muted, and whether it is the headset's.</summary>
    /// <exception cref="PulseException">The sound server could not be asked, or has no such device.</exception>
    public static EndpointInfo Describe(Flow flow)
    {
        using var client = new PulseClient();
        var (device, headset) = Find(client, flow);
        return new EndpointInfo(device.Description, headset, (int)Math.Round(device.Volume * 100), device.Muted);
    }

    /// <summary>Sets the headset's volume, 0 to 100.</summary>
    /// <exception cref="PulseException">The sound server could not be asked, or refused.</exception>
    public static void SetPercent(int percent, Flow flow)
    {
        using var client = new PulseClient();
        var (device, _) = Find(client, flow);
        float volume = Math.Clamp(percent, 0, 100) / 100f;
        if (flow == Flow.Output) client.SetSinkVolume(device, volume);
        else client.SetSourceVolume(device, volume);
    }

    /// <summary>Mutes or unmutes the headset.</summary>
    /// <exception cref="PulseException">The sound server could not be asked, or refused.</exception>
    public static void SetMuted(bool muted, Flow flow)
    {
        using var client = new PulseClient();
        var (device, _) = Find(client, flow);
        if (flow == Flow.Output) client.SetSinkMute(device, muted);
        else client.SetSourceMute(device, muted);
    }

    private static (PulseSink Device, bool Headset) Find(PulseClient client, Flow flow)
    {
        var devices = flow == Flow.Output ? client.Sinks() : client.Sources();
        string listening = flow == Flow.Output ? client.DefaultSink() : client.DefaultSource();

        // The one in use first, for the reason in Windows' version: several
        // devices can answer for the headset, one per transmitter plugged in.
        var headset = devices.FirstOrDefault(d => d.IsHeadset && d.Name == listening)
                      ?? devices.FirstOrDefault(d => d.IsHeadset);
        if (headset is not null) return (headset, true);

        return devices.FirstOrDefault(d => d.Name == listening) is { } fallback
            ? (fallback, false)
            : throw new PulseException("there is no audio device available");
    }
}
