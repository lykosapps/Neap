using Neap.Core.Audio;
using Neap.Core.Audio.Pulse;

namespace Neap.Services;

/// <summary>The headset's volume and mute in the system, as the screens show them.</summary>
/// <param name="Percent">How loud, 0 to 100.</param>
/// <param name="Muted">Whether the system has it muted.</param>
/// <param name="DeviceName">The device's name, or why there is none.</param>
/// <param name="FoundHeadset">Whether the device is the headset's, rather than the default standing in for it.</param>
public sealed record VolumeState(int Percent, bool Muted, string DeviceName, bool FoundHeadset);

/// <summary>The system's volume and mute for the headset's output and its microphone.</summary>
public interface IVolumes
{
    /// <exception cref="Exception">The system could not be asked.</exception>
    EndpointInfo Describe(Flow flow);

    /// <exception cref="Exception">The system refused.</exception>
    void SetPercent(int percent, Flow flow);

    /// <exception cref="Exception">The system refused.</exception>
    void SetMuted(bool muted, Flow flow);
}

/// <summary>Volume and mute from whichever sound system this is.</summary>
public sealed class SystemVolumes : IVolumes
{
    public static SystemVolumes Instance { get; } = new();

    private SystemVolumes() { }

    public EndpointInfo Describe(Flow flow) =>
        OperatingSystem.IsWindows() ? AudioEndpoints.Describe(AudioEndpoints.DefaultMatch, flow)
        : OperatingSystem.IsLinux() ? PulseVolumes.Describe(flow)
        : throw Unsupported();

    public void SetPercent(int percent, Flow flow)
    {
        if (OperatingSystem.IsWindows()) AudioEndpoints.SetPercent(percent, AudioEndpoints.DefaultMatch, flow);
        else if (OperatingSystem.IsLinux()) PulseVolumes.SetPercent(percent, flow);
        else throw Unsupported();
    }

    public void SetMuted(bool muted, Flow flow)
    {
        if (OperatingSystem.IsWindows()) AudioEndpoints.SetMuted(muted, AudioEndpoints.DefaultMatch, flow);
        else if (OperatingSystem.IsLinux()) PulseVolumes.SetMuted(muted, flow);
        else throw Unsupported();
    }

    private static PlatformNotSupportedException Unsupported() =>
        new($"{Environment.OSVersion.Platform} has no sound system to ask");
}

/// <summary>
/// The system's own volume and mute for the headset, off the UI thread.
/// </summary>
/// <remarks>
/// <para>
/// Two of these are not headset settings at all, and the screens say so:
/// master volume and microphone sensitivity belong to the system, and the
/// headset only mirrors them back. Writing the headset's copy changes the
/// number it reports and nothing anybody can hear.
/// </para>
/// <para>
/// Every call touches the system's audio libraries and several enumerate
/// devices, so none of it runs on the UI thread.
/// </para>
/// </remarks>
public static class SoundVolume
{
    public static Task<VolumeState> Read(Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            var info = Pretend.Windows?.Describe(flow) ?? Platform.Current.Volumes.Describe(flow);
            return new VolumeState(info.Percent, info.Muted, info.Name, info.MatchedHeadset);
        }
        catch (Exception)
        {
            return new VolumeState(0, false, Strings.Get("Audio_NoDevice"), false);
        }
    });

    // A set that fails is logged but not shown: the rows re-read the system
    // every second, so the control goes back to the real value on its own.
    public static Task SetVolume(int percent, Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            if (Pretend.Windows is { } windows) windows.SetPercent(percent, flow);
            else Platform.Current.Volumes.SetPercent(percent, flow);
        }
        catch (Exception ex) { AppLog.Write($"could not set the system's {flow} volume: {ex.Message}"); }
    });

    public static Task SetMuted(bool muted, Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            if (Pretend.Windows is { } windows) windows.SetMuted(muted, flow);
            else Platform.Current.Volumes.SetMuted(muted, flow);
        }
        catch (Exception ex) { AppLog.Write($"could not set the system's {flow} mute: {ex.Message}"); }
    });
}
