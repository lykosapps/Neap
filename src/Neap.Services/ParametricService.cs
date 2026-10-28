using Neap.Core.Connection;
using Neap.Core.Presets;

namespace Neap.Services;

/// <summary>
/// Sends the game equaliser's ten bands to the headset, as the latest shape
/// asked for and no more often than the headset's DSP can take them.
/// </summary>
/// <remarks>
/// <para>
/// Only the latest shape is ever waiting to be sent, as with the loudness
/// boost: a point dragged across the curve asks for many shapes and the headset
/// is given the last of them. What the headset holds is forgotten whenever it
/// connects or goes, so the saved shape is sent whole again, when parametric is
/// on. The shape is saved in the app's settings once it stops changing, so it
/// is there to send again after an update from Swarm II puts the stock bands back.
/// </para>
/// <para>
/// Another app, such as the phone app or Swarm II, changes only the ten bands'
/// gain settings, which Neap's own parametric writes never touch. So a change
/// to them while the Parametric mode is in use is another app's, and the shape
/// takes the new gains (<see cref="GainFollow"/>). The gains last seen are kept
/// in the settings, so a change made while Neap was closed is followed when the
/// headset next connects.
/// </para>
/// </remarks>
public sealed class ParametricService
{
    /// <summary>The wait between writes: the handover allows about ten a second.</summary>
    private static readonly TimeSpan Pace = TimeSpan.FromMilliseconds(100);

    private readonly HeadsetService _headset;
    private readonly IUiThread _ui = Platform.Current.Ui;
    private readonly BandSender _sender = new();
    private volatile IReadOnlyList<ParametricBand>? _wanted;
    private int _queued;

    private bool _wasConnected;

    public ParametricService(HeadsetService headset) => _headset = headset;

    /// <summary>The ten bands as the person last left them, or flat, none in use, before they have made any.</summary>
    public static IReadOnlyList<ParametricBand> Saved =>
        StoredBand.Bands(AppSettings.Current.ParametricShape) ?? [.. StockBands.Game.Select(band => band with { InUse = false })];

    /// <summary>Starts sending the saved shape each time the headset connects, while parametric is on, and following another app's gains.</summary>
    public void Watch()
    {
        _headset.StatusChanged += OnStatus;
        _headset.Changed += OnValues;
    }

    /// <summary>The mode or the saved shape changed, so a profile may now read as edited. Raised on the UI thread.</summary>
    public event Action? Changed;

    private void Announce() => _ui.Post(() => Changed?.Invoke());

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
            Set(Followed(Saved));
        }
    }

    /// <summary>Takes another app's change to the gains into the shape, while the Parametric mode is in use.</summary>
    private void OnValues()
    {
        if (!_wasConnected) return;
        var mine = _wanted ?? Saved;
        var bands = Followed(mine);
        if (!ReferenceEquals(bands, mine)) Set(bands);
    }

    /// <summary>
    /// The bands with any gain another app changed since Neap last looked, or
    /// the same bands when none was. Keeps the gains seen either way, and saves
    /// and announces a changed shape.
    /// </summary>
    /// <param name="mine">The ten bands Neap holds.</param>
    private IReadOnlyList<ParametricBand> Followed(IReadOnlyList<ParametricBand> mine)
    {
        if (ReportedGains() is not { } reported) return mine;
        var seen = AppSettings.Current.ParametricSeenGains;
        if (seen.Count == reported.Length && seen.SequenceEqual(reported)) return mine;
        AppSettings.Update(settings => settings.ParametricSeenGains = [.. reported]);

        bool following = AppSettings.Current.ParametricOn && Active && _headset.Model is { HasParametricBands: true };
        if (!following || seen.Count != reported.Length) return mine;
        var result = GainFollow.Follow(mine, seen, reported);
        if (!result.Changed) return mine;

        AppLog.Write($"parametric: another app changed the gain of band {string.Join(", ", result.ChangedBands.Select(band => band + 1))}, and the shape follows it");
        AppSettings.Update(settings => settings.ParametricShape = [.. result.Bands.Select(StoredBand.Of)]);
        _ui.Post(() => ShapeChanged?.Invoke());
        return result.Bands;
    }

    /// <summary>The ten bands' gain settings as the headset reports them, in decibels, or null before all are read.</summary>
    private double[]? ReportedGains()
    {
        var keys = PresetStore.Game.Bands;
        var gains = new double[keys.Count];
        for (int i = 0; i < gains.Length; i++)
        {
            if (!_headset.TryGetNumberByKey(keys[i], out int tenths)) return null;
            gains[i] = tenths / 10.0;
        }

        return gains;
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
            if (Volatile.Read(ref _queued) == 0)
            {
                AppSettings.Update(settings => settings.ParametricShape = [.. latest.Select(StoredBand.Of)]);
                Announce();
            }

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
        if (ReportedGains() is { } reported) AppSettings.Update(settings => settings.ParametricSeenGains = [.. reported]);
        Set(StoredBand.Bands(AppSettings.Current.ParametricShape) ?? StockBands.FromGains(heard));
        Announce();
    }

    /// <summary>Goes back to the ten bands: the stock frequencies, widths and filters, with the gains of the ten-band curve that was set.</summary>
    /// <param name="heard">The ten gains of the ten-band curve, in tenths of a decibel.</param>
    public void Leave(IReadOnlyList<int> heard)
    {
        AppSettings.Update(settings => settings.ParametricActive = false);
        _wanted = StockBands.WithGains(heard);
        Send();
        Announce();
    }

    /// <summary>Forgets what the headset holds, so the next shape is sent whole.</summary>
    public void Forget() => _sender.Forget();

    // -- saved presets -----------------------------------------------------

    /// <summary>The shape or the mode was changed from outside the Equaliser page, by a profile. Raised on the thread that changed it.</summary>
    public event Action? ShapeChanged;

    /// <summary>The parametric shapes saved under names, in the order saved.</summary>
    public static IReadOnlyList<ParametricPreset> Presets =>
        [.. AppSettings.Current.ParametricPresets.Select(p => new ParametricPreset(p.Name, new ParametricShape(p.Bands)))];

    /// <summary>The preset the shape came from, or null when it came from none.</summary>
    public static ParametricPreset? Baseline => ParametricPresets.Find(Presets, AppSettings.Current.ParametricPresetName);

    /// <summary>Marks a preset as the one the shape comes from, or none with null; the bands are the caller's to set.</summary>
    /// <param name="name">The preset's name, or null.</param>
    public static void Choose(string? name) => AppSettings.Update(settings => settings.ParametricPresetName = name);

    /// <summary>Saves the bands under a name, as a new preset or over the one with that name, and marks it as the one in use.</summary>
    /// <param name="name">The name.</param>
    /// <param name="bands">The ten bands.</param>
    public static void Save(string name, IReadOnlyList<ParametricBand> bands)
    {
        StorePresets(ParametricPresets.Saved(Presets, name, ParametricShape.Of(bands)));
        Choose(ParametricPresets.Find(Presets, name)?.Name);
    }

    /// <summary>Renames the preset in use.</summary>
    /// <param name="name">Its new name.</param>
    public static void Rename(string name)
    {
        if (Baseline is not { } preset) return;
        StorePresets(ParametricPresets.Renamed(Presets, preset.Name, name));
        Choose(name.Trim());
    }

    /// <summary>Deletes the preset in use, leaving the shape as it is with no preset behind it.</summary>
    public static void Delete()
    {
        if (Baseline is not { } preset) return;
        StorePresets(ParametricPresets.Without(Presets, preset.Name));
        Choose(null);
    }

    private static void StorePresets(IReadOnlyList<ParametricPreset> presets) => AppSettings.Update(settings =>
        settings.ParametricPresets = [.. presets.Select(p => new AppSettings.StoredParametricPreset { Name = p.Name, Bands = [.. p.Shape.Bands] })]);

    /// <summary>Puts a profile's shape on the headset in the Parametric mode, naming the preset it matches, if any.</summary>
    /// <param name="shape">The shape.</param>
    public void Use(ParametricShape shape)
    {
        if (StoredBand.Bands(shape.Bands) is not { } bands)
        {
            AppLog.Write("parametric: a profile's shape is not ten bands the headset has, so it was not sent");
            return;
        }

        Choose(ParametricPresets.Matching(Presets, shape)?.Name);
        AppSettings.Update(settings => settings.ParametricActive = true);
        Set(bands);
        AppSettings.Update(settings => settings.ParametricShape = [.. shape.Bands]);
        ShapeChanged?.Invoke();
    }

    /// <summary>Goes back to the ten bands for a profile that holds them.</summary>
    /// <param name="heard">The ten gains of the ten-band curve, in tenths of a decibel.</param>
    public void UseBands(IReadOnlyList<int> heard)
    {
        Leave(heard);
        ShapeChanged?.Invoke();
    }
}
