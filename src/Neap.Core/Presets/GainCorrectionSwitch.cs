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

    /// <summary>The headset is not reached over its own cable, where the record is read and written, so nothing was sent.</summary>
    NeedsCable,

    /// <summary>Neap only reads this headset, so nothing was sent.</summary>
    ReadOnly,
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
/// Reading back through a transmitter can answer from memory, so this is for
/// the headset reached over its own cable, where SP2Tools proved the write.
/// </para>
/// </remarks>
public sealed class GainCorrectionSwitch
{
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
