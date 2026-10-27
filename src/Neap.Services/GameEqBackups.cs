using System.Globalization;
using Neap.Core;
using Neap.Core.Presets;

namespace Neap.Services;

/// <summary>
/// Copies of the headset's game equaliser record, kept before Neap changes it.
/// </summary>
/// <remarks>
/// Every copy is kept and none is overwritten, so the oldest is the record as
/// the headset had it before Neap first touched it. They are small, and the one
/// that matters is the one nobody expects to need.
/// </remarks>
public static class GameEqBackups
{
    private const string Stamp = "yyyy-MM-dd HH.mm.ss";

    /// <summary>Where the copies are kept.</summary>
    public static string Folder => Path.Combine(AppFolder.Path, "Equaliser backups");

    /// <summary>Saves a copy of a record and returns where it went.</summary>
    /// <param name="record">The record as the headset holds it.</param>
    /// <exception cref="IOException">The copy could not be saved.</exception>
    public static string Keep(GameEqRecord record) => Keep(record, DateTime.Now);

    /// <summary>Saves a copy of a record under the time it was taken, and returns where it went.</summary>
    /// <param name="record">The record as the headset holds it.</param>
    /// <param name="taken">When it was taken.</param>
    /// <exception cref="IOException">The copy could not be saved.</exception>
    internal static string Keep(GameEqRecord record, DateTime taken)
    {
        Directory.CreateDirectory(Folder);
        string file = Path.Combine(Folder, string.Create(CultureInfo.InvariantCulture, $"{taken.ToString(Stamp, CultureInfo.InvariantCulture)} game equaliser record.bin"));
        File.WriteAllBytes(file, record.Bytes.ToArray());
        return file;
    }
}
