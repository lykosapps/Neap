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
/// connects or goes, so the saved shape is sent whole again.
/// </remarks>
public sealed class ParametricService
{
    /// <summary>The wait between writes: the handover allows about ten a second.</summary>
    private static readonly TimeSpan Pace = TimeSpan.FromMilliseconds(100);

    private readonly HeadsetService _headset;
    private readonly BandSender _sender = new();
    private volatile IReadOnlyList<ParametricBand>? _wanted;
    private int _queued;

    public ParametricService(HeadsetService headset) => _headset = headset;

    /// <summary>Asks for the ten bands to be sent, replacing any shape still waiting.</summary>
    /// <param name="bands">The ten bands.</param>
    public void Set(IReadOnlyList<ParametricBand> bands)
    {
        _wanted = bands;
        if (Interlocked.Exchange(ref _queued, 1) == 1) return;

        var job = _headset.Post(client =>
        {
            Interlocked.Exchange(ref _queued, 0);
            _sender.Send(_wanted!, client.SetBand, () => Thread.Sleep(Pace));
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
