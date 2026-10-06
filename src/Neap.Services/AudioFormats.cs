using Neap.Core.Audio;

namespace Neap.Services;

/// <summary>An endpoint's format on offer, and the one in use, or why neither could be read.</summary>
public sealed record FormatPanel(
    string DeviceName, AudioFormat? Current, IReadOnlyList<AudioFormat> Options, string? Trouble)
{
    public static FormatPanel Empty(string trouble) =>
        new("", null, Array.Empty<AudioFormat>(), trouble);
}

/// <summary>
/// The system's default format for the headset's output and microphone, off
/// the UI thread.
/// </summary>
/// <remarks>
/// <para>
/// Only Windows keeps a format per device that a program can set; the sound
/// server on Linux follows each program's own, so there is nothing to offer
/// there and the screens leave the setting out.
/// </para>
/// <para>
/// Every call touches COM and several enumerate endpoints, so none of it runs
/// on the UI thread.
/// </para>
/// </remarks>
public static class AudioFormats
{
    /// <summary>Whether this system has a format to set.</summary>
    public static bool Supported => OperatingSystem.IsWindows() || Pretend.Windows is not null;

    /// <summary>What the system will accept on this endpoint, and what it is on now.</summary>
    /// <remarks>
    /// The list is probed rather than assumed, and only what the endpoint
    /// actually agrees to is offered. 24-bit is stored packed; a padded 24-bit
    /// format is one the device will not open.
    /// </remarks>
    public static Task<FormatPanel> Read(Flow flow = Flow.Output) => Task.Run(() =>
    {
        try
        {
            var report = Pretend.Windows?.Formats(flow) ?? Describe(flow);
            return new FormatPanel(report.Device, report.Current, report.Options, null);
        }
        catch (Exception ex) { return FormatPanel.Empty(ex.Message); }
    });

    /// <summary>
    /// Changes the endpoint format for real, and checks the device is running
    /// at it; see <see cref="DeviceFormat.Switch"/>.
    /// </summary>
    /// <returns>Null on success, or the reason it failed.</returns>
    public static Task<string?> Apply(AudioFormat format, Flow flow = Flow.Output) =>
        Task.Run<string?>(() =>
        {
            string device = flow == Flow.Output ? "headset output" : "microphone";
            try
            {
                if (Pretend.Windows is { } windows) windows.ApplyFormat(format.Bits, format.Rate, flow);
                else Switch(format, flow);
                AppLog.Write($"format: {device} set to {format.Label}");
                return null;
            }
            catch (Exception ex)
            {
                AppLog.Write($"format: could not set the {device} to {format.Label}: {ex.Message}");
                return ex.Message;
            }
        });

    private static FormatReport Describe(Flow flow) =>
        OperatingSystem.IsWindows()
            ? DeviceFormat.Describe(AudioEndpoints.DefaultMatch, flow)
            : throw new PlatformNotSupportedException("this system has no format to read");

    private static void Switch(AudioFormat format, Flow flow)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("this system has no format to set");
        DeviceFormat.Switch(AudioEndpoints.DefaultMatch, format.Bits, format.Rate, flow);
    }
}
