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

    /// <summary>The ten bands as the person last left them, or flat, none in use, before they have made any.</summary>
    public static IReadOnlyList<ParametricBand> Saved =>
        StoredBand.Bands(AppSettings.Current.ParametricShape) ?? [.. StockBands.Game.Select(band => band with { InUse = false })];

    /// <summary>Starts sending the saved shape each time the headset connects, while parametric is on.</summary>
    public void Watch() => _headset.StatusChanged += OnStatus;

    /// <summary>Parametric was found reset by Swarm II and turned off. Raised on the UI thread.</summary>
    public event Action? Reset;

    private void OnStatus(HeadsetStatus status)
    {
        bool connected = status.Link == Link.Connected;
        if (!connected) _sender.Forget();
        else if (!_wasConnected && AppSettings.Current.ParametricOn && _headset.Model is { HasParametricBands: true })
            CheckThenSend();
        _wasConnected = connected;
    }

    /// <summary>Turns parametric off if Swarm II has reset the headset's equaliser, or else sends the saved shape.</summary>
    /// <remarks>
    /// After a reset the gains are no longer exact, so the shape is not sent
    /// until parametric is turned on again.
    /// </remarks>
    private async void CheckThenSend()
    {
        string? firmware = _headset.Values.TryGetValue("130", out var value) ? value.GetString() : null;
        bool reset;
        try
        {
            reset = await _headset.Post(client => ParametricOff.ResetBySwarm(
                AppSettings.Current.ParametricFirmware, firmware,
                client.OnCable ? client.ReadGameEqRecord()?.GainCorrectionOn : null));
        }
        catch (Exception ex)
        {
            AppLog.Write($"parametric: could not check for a reset by Swarm II: {ex.Message}");
            return;
        }

        if (reset)
        {
            AppLog.Write($"parametric: turned off, as Swarm II reset the headset's equaliser (firmware {firmware ?? "unknown"})");
            AppSettings.Update(settings => (settings.ParametricOn, settings.ParametricFirmware, settings.ParametricReset) = (false, null, true));
            Reset?.Invoke();
        }
        else if (AppSettings.Current.ParametricActive)
        {
            Set(Saved);
        }
    }

    /// <summary>Asks for the ten bands to be sent, replacing any shape still waiting.</summary>
    /// <param name="bands">The ten bands.</param>
    public void Set(IReadOnlyList<ParametricBand> bands)
    {
        _wanted = bands;
        Send();
    }

    /// <summary>Sends the shape waiting, unless a job already is on its way to.</summary>
    private void Send()
    {
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

    /// <summary>Whether the equaliser is in its Parametric mode.</summary>
    public static bool Active => AppSettings.Current.ParametricActive;

    /// <summary>Goes into the Parametric mode, sending the saved shape, or the curve already heard when there is none.</summary>
    /// <param name="heard">The ten gains of the ten-band curve now heard, in tenths of a decibel.</param>
    public void Enter(IReadOnlyList<int> heard)
    {
        AppSettings.Update(settings => settings.ParametricActive = true);
        Set(StoredBand.Bands(AppSettings.Current.ParametricShape) ?? StockBands.FromGains(heard));
    }

    /// <summary>Goes back to the ten bands: the stock frequencies, widths and filters, with the gains of the ten-band curve that was set.</summary>
    /// <param name="heard">The ten gains of the ten-band curve, in tenths of a decibel.</param>
    public void Leave(IReadOnlyList<int> heard)
    {
        AppSettings.Update(settings => settings.ParametricActive = false);
        _wanted = StockBands.WithGains(heard);
        Send();
    }

    /// <summary>Forgets what the headset holds, so the next shape is sent whole.</summary>
    public void Forget() => _sender.Forget();
}
