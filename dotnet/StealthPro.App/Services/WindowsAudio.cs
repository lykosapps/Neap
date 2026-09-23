using StealthPro.Core.Audio;

namespace StealthPro.App.Services;

public sealed record VolumeState(int Percent, bool Muted, string DeviceName, bool FoundHeadset);

public sealed record FormatPanel(
    string DeviceName, AudioFormat? Current, IReadOnlyList<AudioFormat> Options, string? Trouble)
{
    public static FormatPanel Empty(string trouble) =>
        new("", null, Array.Empty<AudioFormat>(), trouble);
}

/// <summary>
/// Windows' own audio settings for the headset, off the UI thread.
///
/// Two of these are not headset settings at all, and the UI says so: master
/// volume and microphone sensitivity belong to Windows, and the headset only
/// mirrors them back. Writing the headset's copy changes the number it
/// reports and nothing anybody can hear.
///
/// Every call here touches COM and several enumerate endpoints, so none of
/// it runs on the UI thread.
/// </summary>
public static class WindowsAudio
{
    public static Task<VolumeState> Read(Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            var info = AudioEndpoints.Describe(AudioEndpoints.DefaultMatch, flow);
            return new VolumeState(info.Percent, info.Muted, info.Name, info.MatchedHeadset);
        }
        catch (Exception)
        {
            return new VolumeState(0, false, Strings.Get("Audio_NoDevice"), false);
        }
    });

    public static Task SetVolume(int percent, Flow flow = Flow.Output) => Task.Run(() =>
    {
        try { AudioEndpoints.SetPercent(percent, AudioEndpoints.DefaultMatch, flow); } catch { }
    });

    public static Task SetMuted(bool muted, Flow flow = Flow.Output) => Task.Run(() =>
    {
        try { AudioEndpoints.SetMuted(muted, AudioEndpoints.DefaultMatch, flow); } catch { }
    });

    /// <summary>
    /// What Windows will accept on this endpoint, and what it is on now.
    ///
    /// The list is probed rather than assumed. An earlier version offered
    /// formats the device would not open, because 24-bit is stored packed
    /// and was being built padded; only what the endpoint actually agrees to
    /// is offered.
    /// </summary>
    public static Task<FormatPanel> Formats(Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            var report = DeviceFormat.Describe(AudioEndpoints.DefaultMatch, flow);
            return new FormatPanel(report.Device, report.Current, report.Options, null);
        }
        catch (Exception ex) { return FormatPanel.Empty(ex.Message); }
    });

    /// <summary>
    /// Change the endpoint format for real: write the property store, then
    /// tell Windows to reconfigure the device. Writing the store alone
    /// changes what Sound settings displays and nothing else, which cost a
    /// round of "it says 24-bit but the light has not gone purple".
    /// </summary>
    public static Task<string?> ApplyFormat(AudioFormat format, Flow flow = Flow.Output) =>
        Task.Run<string?>(() =>
        {
            try
            {
                DeviceFormat.Apply(AudioEndpoints.DefaultMatch, format.Bits, format.Rate, flow);
                return null;
            }
            catch (Exception ex) { return ex.Message; }
        });
}
