using Neap.Core.Audio;

namespace Neap.App.Services;

/// <summary>
/// Windows' own audio settings for the headset, off the UI thread.
/// </summary>
/// <remarks>
/// Two of these are not headset settings at all, and the UI says so: master
/// volume and microphone sensitivity belong to Windows, and the headset only
/// mirrors them back. Writing the headset's copy changes the number it
/// reports and nothing anybody can hear.
/// </remarks>
public static class WindowsAudio
{
    public static Task<VolumeState> Read(Flow flow = Flow.Output) => SoundVolume.Read(flow);

    public static Task SetVolume(int percent, Flow flow = Flow.Output) => SoundVolume.SetVolume(percent, flow);

    public static Task SetMuted(bool muted, Flow flow = Flow.Output) => SoundVolume.SetMuted(muted, flow);

    /// <summary>What Windows will accept on this endpoint, and what it is on now.</summary>
    public static Task<FormatPanel> Formats(Flow flow = Flow.Output) => AudioFormats.Read(flow);

    /// <returns>Null on success, or the reason it failed.</returns>
    public static Task<string?> ApplyFormat(AudioFormat format, Flow flow = Flow.Output) =>
        AudioFormats.Apply(format, flow);
}
