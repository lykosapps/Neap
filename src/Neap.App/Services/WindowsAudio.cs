using Neap.Core.Audio;

namespace Neap.App.Services;

public sealed record VolumeState(int Percent, bool Muted, string DeviceName, bool FoundHeadset);

public sealed record FormatPanel(
    string DeviceName, AudioFormat? Current, IReadOnlyList<AudioFormat> Options, string? Trouble)
{
    public static FormatPanel Empty(string trouble) =>
        new("", null, Array.Empty<AudioFormat>(), trouble);
}

/// <summary>
/// Windows' own audio settings for the headset, off the UI thread.
/// </summary>
/// <remarks>
/// <para>
/// Two of these are not headset settings at all, and the UI says so: master
/// volume and microphone sensitivity belong to Windows, and the headset only
/// mirrors them back. Writing the headset's copy changes the number it
/// reports and nothing anybody can hear.
/// </para>
/// <para>
/// Every call here touches COM and several enumerate endpoints, so none of
/// it runs on the UI thread.
/// </para>
/// </remarks>
public static class WindowsAudio
{
    public static Task<VolumeState> Read(Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            var info = Pretend.Windows?.Describe(flow) ?? AudioEndpoints.Describe(AudioEndpoints.DefaultMatch, flow);
            return new VolumeState(info.Percent, info.Muted, info.Name, info.MatchedHeadset);
        }
        catch (Exception)
        {
            return new VolumeState(0, false, Strings.Get("Audio_NoDevice"), false);
        }
    });

    // A set that fails is logged but not shown: the rows re-read Windows every
    // second, so the control goes back to the real value on its own.
    public static Task SetVolume(int percent, Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            if (Pretend.Windows is { } windows) windows.SetPercent(percent, flow);
            else AudioEndpoints.SetPercent(percent, AudioEndpoints.DefaultMatch, flow);
        }
        catch (Exception ex) { AppLog.Write($"could not set Windows' {flow} volume: {ex.Message}"); }
    });

    public static Task SetMuted(bool muted, Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            if (Pretend.Windows is { } windows) windows.SetMuted(muted, flow);
            else AudioEndpoints.SetMuted(muted, AudioEndpoints.DefaultMatch, flow);
        }
        catch (Exception ex) { AppLog.Write($"could not set Windows' {flow} mute: {ex.Message}"); }
    });

    /// <summary>What Windows will accept on this endpoint, and what it is on now.</summary>
    /// <remarks>
    /// The list is probed rather than assumed, and only what the endpoint
    /// actually agrees to is offered. 24-bit is stored packed; a padded 24-bit
    /// format is one the device will not open.
    /// </remarks>
    public static Task<FormatPanel> Formats(Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            var report = Pretend.Windows?.Formats(flow) ?? DeviceFormat.Describe(AudioEndpoints.DefaultMatch, flow);
            return new FormatPanel(report.Device, report.Current, report.Options, null);
        }
        catch (Exception ex) { return FormatPanel.Empty(ex.Message); }
    });

    /// <summary>
    /// Change the endpoint format for real, and check the device is running
    /// at it; see <see cref="DeviceFormat.Switch"/>.
    /// </summary>
    /// <returns>Null on success, or the reason it failed.</returns>
    public static Task<string?> ApplyFormat(AudioFormat format, Flow flow = Flow.Output) =>
        Task.Run<string?>(() =>
        {
            string device = flow == Flow.Output ? "headset output" : "microphone";
            try
            {
                if (Pretend.Windows is { } windows) windows.ApplyFormat(format.Bits, format.Rate, flow);
                else DeviceFormat.Switch(AudioEndpoints.DefaultMatch, format.Bits, format.Rate, flow);
                AppLog.Write($"format: {device} set to {format.Label}");
                return null;
            }
            catch (Exception ex)
            {
                AppLog.Write($"format: could not set the {device} to {format.Label}: {ex.Message}");
                return ex.Message;
            }
        });
}
