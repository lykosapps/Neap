using Neap.Core.Firmware;
using Neap.Core.Pretend;

namespace Neap.Services;

/// <summary>What reads and writes sections of a headset's firmware, in its download mode.</summary>
/// <remarks>
/// <para>
/// A writer does what it is asked and throws when it cannot.
/// <see cref="FirmwareEditService"/> decides what is written and in what
/// order, saves the backups, reads each write back over the cable and puts a
/// section back as it was when the read back does not match.
/// </para>
/// <para>
/// After a write the program restarts the chip into the firmware, with the
/// cable still in; the chip does not come back in download mode by itself
/// (SP2Tools F318, F319). Another session, or putting a section back, asks
/// for the cable to be plugged in again.
/// </para>
/// </remarks>
public interface IFirmwareWriter
{
    /// <summary>The SHA-256 of each factory section on this headset, by address.</summary>
    IReadOnlyDictionary<int, string> FactorySections { get; }

    /// <summary>Gets whether the chip is in download mode and greeted, so it can be read and written without plugging in again.</summary>
    bool Held { get; }

    /// <summary>Waits until the person has plugged the switched-off headset in, and greets it in download mode.</summary>
    Task WaitForDownloadMode(CancellationToken cancel);

    /// <summary>Reads a whole section in download mode.</summary>
    Task<byte[]> Read(int sector);

    /// <summary>Writes one or two sections; the chip then restarts into the firmware and is no longer held.</summary>
    /// <param name="sections">Each section's address and its bytes, in the order written.</param>
    Task Write(IReadOnlyList<(int Sector, byte[] Bytes)> sections);

    /// <summary>Lets the chip go at the end of a run, so the next run plugs in afresh.</summary>
    void LetGo();
}

/// <summary>Writes a Stealth Pro II's firmware through its chip's download mode.</summary>
/// <remarks>
/// <para>
/// The sections' bytes are loaded at <see cref="WriteProgram.PayloadAddress"/>,
/// the program that writes them at <see cref="WriteProgram.LoadAddress"/>, and
/// the program is started. It erases and programs each section and restarts
/// the chip into the firmware, so the port goes.
/// </para>
/// <para>
/// Closing a port that has gone can hang for about half a minute (F319), so a
/// port is always closed in the background, and the chip is looked for again
/// only once that close is over, so the same port name is free to open.
/// </para>
/// </remarks>
public sealed class BootRomFirmwareWriter : IFirmwareWriter
{
    private BootRom? _chip;
    private Task _closing = Task.CompletedTask;

    public IReadOnlyDictionary<int, string> FactorySections => FirmwareEdit.FactorySections;

    public bool Held => _chip is not null;

    public async Task WaitForDownloadMode(CancellationToken cancel)
    {
        Release();
        if (!_closing.IsCompleted)
        {
            AppLog.Write("firmware: waiting for the last port to close before looking for the chip");
            await _closing.WaitAsync(cancel);
        }
        _chip = await BootRomPort.Open(cancel);
    }

    public Task<byte[]> Read(int sector) => Task.Run(() =>
        Chip.ReadSector(sector) ?? throw new IOException($"section {sector:X8} could not be read in download mode"));

    public async Task Write(IReadOnlyList<(int Sector, byte[] Bytes)> sections)
    {
        var program = WriteProgram.For(sections.Select(section => section.Sector).ToList());
        byte[] payload = [.. sections.SelectMany(section => section.Bytes)];
        var took = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await Task.Run(() =>
            {
                if (!Chip.SendDa(WriteProgram.PayloadAddress, payload)) throw new IOException("the chip did not take the sections' bytes");
                AppLog.Write($"firmware: sections' bytes loaded in {took.ElapsedMilliseconds} ms");
                if (!Chip.SendDa(WriteProgram.LoadAddress, program)) throw new IOException("the chip did not take the program that writes them");
                AppLog.Write($"firmware: write program loaded at {took.ElapsedMilliseconds} ms");
                // The program restarts the chip when it has written, so the port can go mid-answer. That is the write
                // finishing, not failing (SP2Tools F318); the read back decides.
                try
                {
                    Chip.JumpDa(WriteProgram.LoadAddress | 1);
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    AppLog.Write($"firmware: the port went as the write program started ({ex.Message})");
                }
                AppLog.Write($"firmware: write program started at {took.ElapsedMilliseconds} ms");
            });
        }
        finally
        {
            // Started or not, the chip is not to be used again without plugging in afresh.
            Release();
        }
    }

    public void LetGo() => Release();

    private BootRom Chip => _chip ?? throw new InvalidOperationException("the headset is not in download mode");

    /// <summary>Lets the chip go, closing its port in the background, after any close already under way.</summary>
    private void Release()
    {
        if (_chip is not { } gone) return;
        _chip = null;
        var before = _closing;
        _closing = Task.Run(async () =>
        {
            await before;
            try
            {
                gone.Dispose();
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                AppLog.Write($"firmware: closing the port: {ex.Message}");
            }
        });
    }
}
/// <summary>The pretend headset's firmware: the whole flow, with a pause at each step, and nothing real written.</summary>
/// <remarks>
/// It reads and writes the pretend headset's <see cref="PretendFlash"/>, which
/// the headset's flash reads answer from, so a run cut short shows as it would
/// on a headset. The person plugging the headset in is a short wait.
/// </remarks>
/// <param name="flash">The pretend headset's flash.</param>
/// <param name="lost">Whether every write is lost, so a write that does not read back, and Neap putting the section back, can be seen.</param>
public sealed class PretendFirmwareWriter(PretendFlash flash, bool lost) : IFirmwareWriter
{
    private static readonly TimeSpan Pause = TimeSpan.FromMilliseconds(600);
    private static readonly TimeSpan Plugging = TimeSpan.FromSeconds(3);
    // Unplugging and plugging in again takes the person longer, and leaves time to press Stop in a check.
    private static readonly TimeSpan PluggingAgain = TimeSpan.FromSeconds(8);
    private bool _wrote;
    private int _writes;

    public IReadOnlyDictionary<int, string> FactorySections => flash.FactorySections;

    public bool Held { get; private set; }

    public void LetGo() => (Held, _wrote) = (false, false);

    public async Task WaitForDownloadMode(CancellationToken cancel)
    {
        await Task.Delay(_wrote ? PluggingAgain : Plugging, cancel);
        Held = true;
    }

    public async Task<byte[]> Read(int sector)
    {
        await Task.Delay(Pause);
        return flash.Read(sector);
    }

    public async Task Write(IReadOnlyList<(int Sector, byte[] Bytes)> sections)
    {
        await Task.Delay(Pause * 2);
        Held = false;
        _wrote = true;
        // Every other write is lost, so the put back that follows lands and the headset is left as it was.
        if (lost && ++_writes % 2 == 1) return;
        foreach (var (sector, bytes) in sections) flash.Write(sector, bytes);
    }
}
