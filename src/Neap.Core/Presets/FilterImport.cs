using System.Globalization;
using System.Text.RegularExpressions;

namespace Neap.Core.Presets;

/// <summary>What reading a file of filters came to.</summary>
/// <param name="Bands">The filters that fit, in the order they came, at most ten.</param>
/// <param name="NotImported">The lines that were not used, each with its reason.</param>
/// <param name="Notes">What changed on the way: gains held to the limit, and a preamp not used.</param>
/// <param name="PreampDb">The preamp the file asked for, or null when it gave none.</param>
public sealed record FilterImportResult(
    IReadOnlyList<ParametricBand> Bands, IReadOnlyList<string> NotImported, IReadOnlyList<string> Notes, double? PreampDb)
{
    /// <summary>The ten bands to set: the filters that fit, then the rest not in use at their stock frequencies.</summary>
    public IReadOnlyList<ParametricBand> ToTen() =>
        [.. Bands.Concat(StockBands.Game.Skip(Bands.Count).Select(band => band with { InUse = false }))];
}

/// <summary>
/// Reads filters in the text format of Equalizer APO and AutoEq, such as
/// <c>Filter 1: ON PK Fc 105 Hz Gain -2.5 dB Q 0.7</c>.
/// </summary>
/// <remarks>
/// Only what the headset can do is taken. A line that is not understood, a
/// filter that is off, a type the headset does not have, and every filter
/// after the tenth are listed and not used, so nothing is dropped quietly. A
/// gain past the limit is held to it, and a preamp line is reported and left to
/// the person, since the headset's level is the Loudness boost's to set.
/// </remarks>
public static partial class FilterImport
{
    private static readonly Dictionary<string, BandType> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        ["PK"] = BandType.Peaking,
        ["PEQ"] = BandType.Peaking,
        ["LSC"] = BandType.LowShelf,
        ["LS"] = BandType.LowShelf,
        ["HSC"] = BandType.HighShelf,
        ["HS"] = BandType.HighShelf,
        ["HP"] = BandType.LowCut,
        ["HPQ"] = BandType.LowCut,
        ["LP"] = BandType.HighCut,
        ["LPQ"] = BandType.HighCut,
        ["NO"] = BandType.Notch,
    };

    /// <summary>The filters in some text.</summary>
    /// <param name="text">The text of a filter file.</param>
    public static FilterImportResult Parse(string text)
    {
        var bands = new List<ParametricBand>();
        var skipped = new List<string>();
        var notes = new List<string>();
        double? preamp = null;

        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;

            if (PreampLine().Match(line) is { Success: true } pre)
            {
                preamp = Number(pre.Groups[1].Value);
                continue;
            }

            if (FilterLine().Match(line) is not { Success: true } match)
            {
                skipped.Add($"{line} (not understood)");
                continue;
            }

            if (!match.Groups[1].Value.Equals("ON", StringComparison.OrdinalIgnoreCase)) { skipped.Add($"{line} (switched off)"); continue; }
            if (!Types.TryGetValue(match.Groups[2].Value, out var type)) { skipped.Add($"{line} (type {match.Groups[2].Value.ToUpperInvariant()} is not one the headset has)"); continue; }
            if (bands.Count >= BandWrites.Bands) { skipped.Add($"{line} (more than ten bands)"); continue; }

            double gain = match.Groups[4].Success ? Number(match.Groups[4].Value) : 0;
            if (Math.Abs(gain) > ParametricBand.MostGain) notes.Add($"A gain of {gain:0.#} dB was held to {Math.Sign(gain) * ParametricBand.MostGain:+0;-0} dB.");
            double q = match.Groups[5].Success ? Number(match.Groups[5].Value) : DefaultQ(type);
            bands.Add(new ParametricBand(type, Number(match.Groups[3].Value), q, gain, true).Held());
        }

        if (preamp is { } level) notes.Add($"The file's preamp of {level:0.#} dB is not used. Set the level with the Loudness boost.");
        return new FilterImportResult(bands, skipped, notes, preamp);
    }

    /// <summary>The width a filter takes when the file gives none: a bell and a notch an octave, the rest the flat one.</summary>
    private static double DefaultQ(BandType type) => type is BandType.Peaking or BandType.Notch ? StockBands.Q : 0.7071;

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^preamp:\s*(-?[0-9.]+)\s*db", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PreampLine();

    [GeneratedRegex(@"^filter\s*\d*:\s*(on|off)\s+([a-z]+)\s+fc\s+([0-9.]+)\s*hz(?:\s+gain\s+(-?[0-9.]+)\s*db)?(?:\s+q\s+([0-9.]+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FilterLine();
}
