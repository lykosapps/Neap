using System.Globalization;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Presets;

namespace Neap.Services;

/// <summary>
/// Turns the true parametric equaliser on and off: the headset's gain
/// correction cleared for exact gains, or put back with the stock bands.
/// </summary>
/// <remarks>
/// <para>
/// The work is <see cref="GainCorrectionSwitch"/>'s. This runs it on the
/// headset's own thread, over the cable or through the dock, only where the
/// headset can be written to and its battery allows it, keeps a copy of the
/// record first, says each outcome in
/// the app log, and remembers in the app's settings that parametric is on, and
/// which firmware the headset had when it was, so a later update can be told.
/// </para>
/// <para>
/// The bands Neap sent are forgotten after either, so the saved shape is sent
/// whole the next time; turning on in the Parametric mode sends it at once, as
/// after a reset by Swarm II the headset holds the stock bands.
/// </para>
/// </remarks>
public sealed class ParametricSetup
{
    /// <summary>How long the headset is given to settle before the record is read back.</summary>
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(500);

    private readonly HeadsetService _headset;
    private readonly ParametricService _bands;

    public ParametricSetup(HeadsetService headset, ParametricService bands)
    {
        _headset = headset;
        _bands = bands;
    }

    /// <summary>Clears the gain correction, so the parametric equaliser's gains are exact.</summary>
    public Task<SwitchResult> TurnOn() => Run("turn parametric on", true, change => change.TurnOff());

    /// <summary>Puts the stock bands and the gain correction back, keeping the gains.</summary>
    public Task<SwitchResult> Restore() => Run("restore the stock equaliser", false, change => change.ReturnToStock());

    private Task<SwitchResult> Run(string what, bool on, Func<GainCorrectionSwitch, SwitchResult> change)
    {
        if (!ParametricService.Offered)
        {
            AppLog.Write($"parametric: {what}: not in this release, so nothing was sent");
            return Task.FromResult(new SwitchResult(SwitchOutcome.ReadOnly));
        }

        // A job posted with no headset connected would wait for one, so it is answered here.
        if (_headset.Status.Link is Link.Absent or Link.Connecting) return Task.FromResult(Said(what, new SwitchResult(SwitchOutcome.NoHeadset)));
        if (!GainCorrectionSwitch.BatteryAllows(_headset.Battery)) return Task.FromResult(Said(what, new SwitchResult(SwitchOutcome.LowBattery)));

        return _headset.Post(client =>
        {
            if (_headset.ReadOnly) return Said(what, new SwitchResult(SwitchOutcome.ReadOnly));

            // Proven on one firmware; a headset that cannot report a band's
            // frequency is not known to take the live bands, so nothing is
            // written to it. About half of single reads get no reply, so it
            // is asked again before giving up.
            if (on && !AnswersBands(client))
                return Said(what, new SwitchResult(SwitchOutcome.NoLiveBands));

            var result = change(new GainCorrectionSwitch(
                client.ReadGameEqRecord,
                client.WriteGameEqRecord,
                record => AppLog.Write($"parametric: kept a copy of the record at {GameEqBackups.Keep(record)}"),
                () => Thread.Sleep(Settle)));
            if (result.Outcome is SwitchOutcome.Done or SwitchOutcome.AlreadyDone)
            {
                string? build = _headset.GetText("firmware_version");
                AppSettings.Update(settings =>
                {
                    settings.ParametricOn = on;
                    settings.ParametricFirmware = on ? build : null;
                    settings.ParametricReset = false;
                    if (!on) settings.ParametricActive = false;
                });
                _bands.Forget();
                if (on && ParametricService.Active && ParametricService.SavedToSend() is { } saved) _bands.Set(saved);
            }

            return Said(what, result);
        });
    }

    /// <summary>How many times the first band's frequency is asked for before the headset is taken not to have live bands.</summary>
    private const int BandTries = 3;

    /// <summary>Whether the headset reports its first band's frequency, as a firmware that takes the live bands does.</summary>
    private static bool AnswersBands(HeadsetClient client)
    {
        for (int attempt = 0; attempt < BandTries; attempt++)
        {
            if (client.ReadBand(0, BandParameter.Frequency) is { } hertz)
            {
                AppLog.Write(string.Create(CultureInfo.InvariantCulture, $"parametric: the headset reports band 1 at {hertz:0.#} Hz, so it takes the live bands"));
                return true;
            }
        }
        AppLog.Write($"parametric: the headset gave no reading of band 1 in {BandTries} tries, so its firmware is not known to take the live bands");
        return false;
    }

    private static SwitchResult Said(string what, SwitchResult result)
    {
        AppLog.Write($"parametric: {what}: {result.Outcome}{(result.BackupRestored ? ", and the earlier record was put back" : string.Empty)}");
        return result;
    }
}
