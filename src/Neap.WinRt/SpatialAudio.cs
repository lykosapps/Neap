using Neap.Core.Audio;
using Neap.Core.Pretend;
using Windows.Foundation;
using Windows.Media.Audio;

namespace Neap.WinRt;

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

    /// <remarks>
    /// A pretend run has one endpoint and cannot simulate a second
    /// transmitter, so <see cref="SpatialPanel.EndpointId"/> never changes.
    /// </remarks>
    public static Task<SpatialPanel> Read() => Task.Run(() =>
    {
        if (Pretend.Windows is { } windows)
            return new SpatialPanel(SpatialSound.Offered(PretendWindows.SpatialSupported),
                SpatialSound.Of(windows.Spatial), null, PretendWindows.OutputName);
        try
        {
            string? id = AudioEndpoints.HeadsetId(Flow.Output);
            if (id is null || Configuration(id) is not { } configuration)
                return SpatialPanel.Unavailable(Strings.Get("Spatial_NoHeadset"));
            if (!configuration.IsSpatialAudioSupported)
                return SpatialPanel.Unavailable(Strings.Get("Spatial_NotSupported"));
            Guid activeSubtype = Guid.TryParse(configuration.ActiveSpatialAudioFormat, out var parsed) ? parsed : Guid.Empty;
            var active = SpatialSound.Of(activeSubtype);
            return new SpatialPanel(
                SpatialSound.Offered(subtype => configuration.IsSpatialAudioFormatSupported(Text(subtype))),
                active, null, id, Unrecognised: active is null && activeSubtype != Guid.Empty);
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
            var configuration = await Task.Run(() =>
                AudioEndpoints.HeadsetId(Flow.Output) is { } id ? Configuration(id) : null);
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

    /// <summary>
    /// Watches one endpoint for a change made outside Neap, such as from
    /// Sound settings itself. Null when there is nothing to watch: a
    /// pretend run, which has no such event, or the headset not being there.
    /// </summary>
    /// <remarks><paramref name="changed"/> arrives off the UI thread.</remarks>
    public static IDisposable? Watch(string endpointId, Action changed)
    {
        if (Pretend.Windows is not null) return null;
        try
        {
            var configuration = Configuration(endpointId);
            TypedEventHandler<SpatialAudioDeviceConfiguration, object> handler = (_, _) => changed();
            configuration.ConfigurationChanged += handler;
            return new Subscription(configuration, handler);
        }
        catch (Exception ex)
        {
            AppLog.Write($"spatial sound: could not watch for changes: {ex.Message}");
            return null;
        }
    }

    private sealed class Subscription(
        SpatialAudioDeviceConfiguration configuration,
        TypedEventHandler<SpatialAudioDeviceConfiguration, object> handler) : IDisposable
    {
        public void Dispose() => configuration.ConfigurationChanged -= handler;
    }

    /// <summary>The spatial configuration for one of the headset's endpoints.</summary>
    private static SpatialAudioDeviceConfiguration Configuration(string endpointId) =>
        SpatialAudioDeviceConfiguration.GetForDeviceId($@"\\?\SWD#MMDEVAPI#{endpointId}#{RenderInterface}");

    /// <summary>A format as Windows writes it: the identifier in braces.</summary>
    private static string Text(Guid subtype) => subtype.ToString("B").ToUpperInvariant();
}
