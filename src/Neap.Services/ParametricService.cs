using Neap.Core.Connection;
using Neap.Core.Presets;

namespace Neap.Services;

/// <summary>
/// Sends the game equaliser's ten bands to the headset, as the latest shape
/// asked for and no more often than the headset's DSP can take them.
/// </summary>
/// <remarks>
/// Only the latest shape is ever waiting to be sent, as with the loudness
/// boost: a point dragged across the curve asks for many shapes and the headset
/// is given the last of them. What the headset holds is forgotten whenever it
/// connects or goes, so the saved shape is sent whole again, when parametric is
/// on. The shape is saved in the app's settings once it stops changing, so it
/// is there to send again after an update from Swarm II puts the stock bands back.
/// </remarks>
public sealed class ParametricService
{
    /// <summary>The wait between writes: the handover allows about ten a second.</summary>
    private static readonly TimeSpan Pace = TimeSpan.FromMilliseconds(100);

    private readonly HeadsetService _headset;
    private readonly BandSender _sender = new();
    private volatile IReadOnlyList<ParametricBand>? _wanted;
    private int _queued;

    private bool _wasConnected;

    public ParametricService(HeadsetService headset) => _headset = headset;

    /// <summary>The ten bands as the person last left them, or the stock bands before they have made any.</summary>
    public static IReadOnlyList<ParametricBand> Saved => StoredBand.Bands(AppSettings.Current.ParametricShape) ?? StockBands.Game;

    /// <summary>Starts sending the saved shape each time the headset connects, while parametric is on.</summary>
    public void Watch() => _headset.StatusChanged += OnStatus;

    private void OnStatus(HeadsetStatus status)
    {
        bool connected = status.Link == Link.Connected;
        if (!connected) _sender.Forget();
        else if (!_wasConnected && AppSettings.Current.ParametricOn && _headset.Model is { HasParametricBands: true })
            Set(Saved);
        _wasConnected = connected;
    }

    /// <summary>Asks for the ten bands to be sent, replacing any shape still waiting.</summary>
    /// <param name="bands">The ten bands.</param>
    public void Set(IReadOnlyList<ParametricBand> bands)
    {
        _wanted = bands;
        if (Interlocked.Exchange(ref _queued, 1) == 1) return;

        var job = _headset.Post(client =>
        {
            Interlocked.Exchange(ref _queued, 0);
            var latest = _wanted!;
            _sender.Send(latest, client.SetBand, () => Thread.Sleep(Pace));
            if (Volatile.Read(ref _queued) == 0) AppSettings.Update(settings => settings.ParametricShape = [.. latest.Select(StoredBand.Of)]);
            return true;
        });

        // A job that failed before it ran never clears its own mark, which would stop every later change.
        job.ContinueWith(failed =>
        {
            Interlocked.Exchange(ref _queued, 0);
            _sender.Forget();
            if (failed.Exception?.GetBaseException() is { } ex)
                AppLog.Write($"parametric: the bands were not all sent: {ex.Message}");
        }, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
    }

    /// <summary>Forgets what the headset holds, so the next shape is sent whole.</summary>
    public void Forget() => _sender.Forget();
}
