using Neap.Core.Connection;

namespace Neap.Core.Presets;

/// <summary>What turning the gain correction off or back on came to.</summary>
public enum SwitchOutcome
{
    /// <summary>It was written and read back as asked.</summary>
    Done,

    /// <summary>It was already as asked, so nothing was written.</summary>
    AlreadyDone,

    /// <summary>The record could not be read, or was not one Neap understands, so nothing was written.</summary>
    CouldNotRead,

    /// <summary>The write failed.</summary>
    WriteFailed,

    /// <summary>The write was sent but reading the record back did not show it.</summary>
    NotConfirmed,

    /// <summary>No headset is connected, so nothing was sent.</summary>
    NoHeadset,

    /// <summary>The headset's battery is too low to risk the write, so nothing was sent.</summary>
    LowBattery,

    /// <summary>Neap only reads this headset, so nothing was sent.</summary>
    ReadOnly,

    /// <summary>The headset gave no reading of a band, so its firmware is not known to take live bands, and nothing was sent.</summary>
    NoLiveBands,
}

/// <summary>What happened, and whether the record the headset had before was put back.</summary>
/// <param name="Outcome">How it went.</param>
/// <param name="BackupRestored">Whether the earlier record was written back after a failure, and read back as the earlier record.</param>
public readonly record struct SwitchResult(SwitchOutcome Outcome, bool BackupRestored = false);

/// <summary>
/// Turns the headset's stock gain correction off, for exact gains with the
/// parametric equaliser, or puts it back with the stock bands.
/// </summary>
/// <remarks>
/// <para>
/// This rewrites a stored record that the headset keeps, so it is careful in
/// the way the Firmware page's edits are: the record is read and understood
/// first, a copy is kept before anything is written, only the intended change
/// is made, the result is read back, and anything short of exactly what was
/// asked is undone from the copy.
/// </para>
/// <para>
/// The write is proven over the cable (SP2Tools) and through the Charging
/// Dock (28 October 2026), where the record read back before the write was the
/// headset's own curve, not one a transmitter keeps.
/// </para>
/// </remarks>
public sealed class GainCorrectionSwitch
{
    /// <summary>The lowest battery, as a percentage, at which the record is written when the headset is not charging.</summary>
    public const int LowestBattery = 10;

    /// <summary>Whether the battery allows the write: charging, at <see cref="LowestBattery"/> or above, or not known.</summary>
    /// <remarks>
    /// The headset keeps its old record until a new one is written whole, but
    /// what it does after losing power part way through is not known, so a
    /// battery about to run out is not risked. A reading not yet known does not
    /// stop the switch, as a connected headset reports one within moments.
    /// </remarks>
    /// <param name="battery">The headset's battery, or null when it has not been read.</param>
    public static bool BatteryAllows(BatteryReading? battery) =>
        battery is not { } reading || reading.Charging || reading.Percent >= LowestBattery;

    private readonly Func<GameEqRecord?> _read;
    private readonly Action<GameEqRecord> _write;
    private readonly Action<GameEqRecord> _keepBackup;
    private readonly Action _pause;

    /// <param name="read">Reads the headset's record, or null when it gives none.</param>
    /// <param name="write">Writes a whole record.</param>
    /// <param name="keepBackup">Keeps a copy of the record before it is changed; it throws when it cannot, and then nothing is written.</param>
    /// <param name="pause">Waits for the headset to settle before reading back.</param>
    public GainCorrectionSwitch(
        Func<GameEqRecord?> read, Action<GameEqRecord> write, Action<GameEqRecord> keepBackup, Action pause)
    {
        _read = read;
        _write = write;
        _keepBackup = keepBackup;
        _pause = pause;
    }

    /// <summary>Turns the correction off, leaving every other byte of the record as it is.</summary>
    public SwitchResult TurnOff() => Change(record => record.GainCorrectionOn ? record.WithGainCorrection(false) : null);

    /// <summary>Puts the stock bands and the correction back, keeping the gains the record holds.</summary>
    public SwitchResult ReturnToStock() => Change(record => record.GainCorrectionOn && StockBands.Match(record.Bands) ? null : record.ReturnedToStock());

    private SwitchResult Change(Func<GameEqRecord, GameEqRecord?> target)
    {
        if (_read() is not { } before) return new SwitchResult(SwitchOutcome.CouldNotRead);
        if (target(before) is not { } wanted) return new SwitchResult(SwitchOutcome.AlreadyDone);

        _keepBackup(before);
        try
        {
            _write(wanted);
        }
        catch (Exception)
        {
            return new SwitchResult(SwitchOutcome.WriteFailed, PutBack(before));
        }

        _pause();
        if (_read() is { } after && after.Matches(wanted)) return new SwitchResult(SwitchOutcome.Done);
        return new SwitchResult(SwitchOutcome.NotConfirmed, PutBack(before));
    }

    /// <summary>Writes the earlier record back and says whether it then reads back as that record.</summary>
    private bool PutBack(GameEqRecord before)
    {
        try
        {
            _write(before);
            _pause();
            return _read() is { } now && now.Matches(before);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
