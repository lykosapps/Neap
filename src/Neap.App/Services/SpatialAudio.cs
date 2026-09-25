using Neap.Core.Audio;
using Neap.Core.Pretend;
using Windows.Media.Audio;

namespace Neap.App.Services;

/// <summary>The spatial formats on offer for the headset and the one on, or why there are none.</summary>
public sealed record SpatialPanel(IReadOnlyList<SpatialFormat> Offered, SpatialFormat? Active, string? Trouble)
{
    public static SpatialPanel Unavailable(string trouble) => new([], null, trouble);
}

/// <summary>Windows' spatial sound for the headset, off the UI thread.</summary>
/// <remarks>
/// <para>
/// The same setting as Spatial sound in the headset's properties in Sound
/// settings, through Windows' own spatial audio configuration. It is set on
/// the headset's endpoint, never the default device in its place.
/// </para>
/// <para>
/// Windows reports what is installed, not what is licensed; see
/// <see cref="SpatialSound"/>.
/// </para>
/// </remarks>
public static class SpatialAudio
{
    /// <summary>The device interface class Windows gives every audio output.</summary>
    private const string RenderInterface = "{e6327cad-dcec-4949-ae8a-991e976a79d2}";

    public static Task<SpatialPanel> Read() => Task.Run(() =>
    {
        if (Pretend.Windows is { } windows)
            return new SpatialPanel(SpatialSound.Offered(PretendWindows.SpatialSupported),
                SpatialSound.Of(windows.Spatial), null);
        try
        {
            if (Configuration() is not { } configuration)
                return SpatialPanel.Unavailable(Strings.Get("Spatial_NoHeadset"));
            if (!configuration.IsSpatialAudioSupported)
                return SpatialPanel.Unavailable(Strings.Get("Spatial_NotSupported"));
            return new SpatialPanel(
                SpatialSound.Offered(subtype => configuration.IsSpatialAudioFormatSupported(Text(subtype))),
                SpatialSound.Of(Guid.TryParse(configuration.ActiveSpatialAudioFormat, out var active) ? active : Guid.Empty),
                null);
        }
        catch (Exception ex)
        {
            AppLog.Write($"spatial sound: could not read: {ex.Message}");
            return SpatialPanel.Unavailable(ex.Message);
        }
    });

    public static async Task<SpatialResult> Apply(SpatialFormat format)
    {
        var subtype = SpatialSound.SubtypeOf(format);
        if (Pretend.Windows is { } windows) return windows.SetSpatial(subtype);
        try
        {
            var configuration = await Task.Run(Configuration);
            if (configuration is null) return SpatialResult.NotSupportedOnAudioEndpoint;
            var result = await configuration.SetDefaultSpatialAudioFormatAsync(Text(subtype));
            return result.Status switch
            {
                SetDefaultSpatialAudioFormatStatus.Succeeded => SpatialResult.Succeeded,
                SetDefaultSpatialAudioFormatStatus.AccessDenied => SpatialResult.AccessDenied,
                SetDefaultSpatialAudioFormatStatus.LicenseExpired => SpatialResult.LicenseExpired,
                SetDefaultSpatialAudioFormatStatus.LicenseNotValidForAudioEndpoint => SpatialResult.LicenseNotValidForAudioEndpoint,
                SetDefaultSpatialAudioFormatStatus.NotSupportedOnAudioEndpoint => SpatialResult.NotSupportedOnAudioEndpoint,
                _ => SpatialResult.UnknownError,
            };
        }
        catch (Exception ex)
        {
            AppLog.Write($"spatial sound: could not set {format}: {ex.Message}");
            return SpatialResult.UnknownError;
        }
    }

    /// <summary>The headset's spatial configuration, or null when the headset is not there.</summary>
    private static SpatialAudioDeviceConfiguration? Configuration() =>
        AudioEndpoints.HeadsetId(Flow.Output) is { } id
            ? SpatialAudioDeviceConfiguration.GetForDeviceId($@"\\?\SWD#MMDEVAPI#{id}#{RenderInterface}")
            : null;

    /// <summary>A format as Windows writes it: the identifier in braces.</summary>
    private static string Text(Guid subtype) => subtype.ToString("B").ToUpperInvariant();
}
