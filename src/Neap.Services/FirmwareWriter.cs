using Neap.Core.Connection;
using Neap.Core.Firmware;
using Neap.Core.Pretend;

namespace Neap.Services;

/// <summary>What reads and writes sections of a headset's firmware, in its download mode.</summary>
/// <remarks>
/// <para>
/// A writer does what it is asked and throws when it cannot.
/// <see cref="FirmwareEditService"/> decides what is written and in what
/// order, saves the backups, reads each write back and puts a section back
/// as it was when the read back does not match.
/// </para>
/// <para>
/// After a write the program restarts the chip into the firmware, with the
/// cable still in, and <see cref="FirmwareEditService"/> reads the sections
/// back over the headset's own connection. Another session, or putting a
/// section back, asks for download mode again.
/// </para>
/// </remarks>
public interface IFirmwareWriter
{
    /// <summary>The SHA-256 of each factory section on this headset, by address.</summary>
    IReadOnlyDictionary<int, string> FactorySections { get; }

    /// <summary>Waits until the person has plugged the switched-off headset in, and greets it in download mode.</summary>
    Task WaitForDownloadMode(CancellationToken cancel);

    /// <summary>Reads a whole section.</summary>
    Task<byte[]> Read(int sector);

    /// <summary>Writes one or two sections; the chip then restarts into the firmware.</summary>
    /// <param name="sections">Each section's address and its bytes, in the order written.</param>
    /// <param name="percent">Told how far the write has got, from 0 to 100.</param>
    Task Write(IReadOnlyList<(int Sector, byte[] Bytes)> sections, IProgress<int> percent);

    /// <summary>Lets the headset go, and waits until it is back, connected as usual.</summary>
    Task WaitForHeadset(CancellationToken cancel);
}

/// <summary>Writes a Stealth Pro II's firmware through its chip's download mode.</summary>
/// <remarks>
/// <para>
/// The sections' bytes are loaded at <see cref="WriteProgram.PayloadAddress"/>,
/// the program that writes them at <see cref="WriteProgram.LoadAddress"/>, and
/// the program is started. It erases and programs each section and restarts
/// the chip into the firmware, so the port goes. Closing a port that has gone
/// can hang for half a minute, so it is closed in the background (F319).
/// </para>
/// <para>
/// After the person unplugs, the chip's session ends on its own in about 30
/// seconds and the headset starts by itself (F292).
/// </para>
/// </remarks>
/// <param name="headset">The headset's connection as usual, to see it come back.</param>
public sealed class BootRomFirmwareWriter(HeadsetService headset) : IFirmwareWriter
{
    private static readonly TimeSpan HeadsetBack = TimeSpan.FromMinutes(2);

    private BootRom? _chip;

    public IReadOnlyDictionary<int, string> FactorySections => FirmwareEdit.FactorySections;

    public async Task WaitForDownloadMode(CancellationToken cancel)
    {
        Release();
        _chip = await BootRomPort.Open(cancel);
    }

    public Task<byte[]> Read(int sector) => Task.Run(() =>
        Chip.ReadSector(sector) ?? throw new IOException($"section {sector:X8} could not be read in download mode"));

    public async Task Write(IReadOnlyList<(int Sector, byte[] Bytes)> sections, IProgress<int> percent)
    {
        var program = WriteProgram.For(sections.Select(section => section.Sector).ToList());
        byte[] payload = [.. sections.SelectMany(section => section.Bytes)];
        var took = System.Diagnostics.Stopwatch.StartNew();
        await Task.Run(() =>
        {
            percent.Report(0);
            if (!Chip.SendDa(WriteProgram.PayloadAddress, payload)) throw new IOException("the chip did not take the sections' bytes");
            AppLog.Write($"firmware: sections' bytes loaded in {took.ElapsedMilliseconds} ms");
            percent.Report(40);
            if (!Chip.SendDa(WriteProgram.LoadAddress, program)) throw new IOException("the chip did not take the program that writes them");
            AppLog.Write($"firmware: write program loaded at {took.ElapsedMilliseconds} ms");
            percent.Report(50);
            // The program restarts the chip when it has written, so the port can go mid-answer. That is the write
            // finishing, not failing (SP2Tools F318); the read back after the restart decides.
            try
            {
                Chip.JumpDa(WriteProgram.LoadAddress | 1);
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or TimeoutException)
            {
                AppLog.Write($"firmware: the port went as the write program started ({ex.Message})");
            }
            AppLog.Write($"firmware: write program started at {took.ElapsedMilliseconds} ms");
        });
        // Closing a port that has gone can hang for half a minute (3 November 2026: 32 s), and the chip is back in
        // download mode for only half a second after it restarts, so the port is closed in the background and the
        // chip looked for at once.
        var gone = _chip;
        _chip = null;
        _ = Task.Run(() =>
        {
            try
            {
                gone?.Dispose();
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
            {
                AppLog.Write($"firmware: closing the port after the write: {ex.Message}");
            }
        });

        percent.Report(100);
    }

    /// <remarks>A headset not back in time is left for the confirming read to find unreadable, rather than failing here.</remarks>
    public async Task WaitForHeadset(CancellationToken cancel)
    {
        Release();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (headset.Status.Link != Link.Connected && clock.Elapsed < HeadsetBack) await Task.Delay(250, cancel);
    }

    private BootRom Chip => _chip ?? throw new InvalidOperationException("the headset is not in download mode");

    private void Release()
    {
        _chip?.Dispose();
        _chip = null;
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
    private int _writes;

    public IReadOnlyDictionary<int, string> FactorySections => flash.FactorySections;

    public Task WaitForDownloadMode(CancellationToken cancel) => Task.Delay(Plugging, cancel);

    public async Task<byte[]> Read(int sector)
    {
        await Task.Delay(Pause);
        return flash.Read(sector);
    }

    public async Task Write(IReadOnlyList<(int Sector, byte[] Bytes)> sections, IProgress<int> percent)
    {
        for (int done = 0; done <= 100; done += 10)
        {
            percent.Report(done);
            await Task.Delay(Pause / 6);
        }
        // Every other write is lost, so the put back that follows lands and the headset is left as it was.
        if (lost && ++_writes % 2 == 1) return;
        foreach (var (sector, bytes) in sections) flash.Write(sector, bytes);
    }

    public Task WaitForHeadset(CancellationToken cancel) => Task.Delay(Plugging, cancel);
}
