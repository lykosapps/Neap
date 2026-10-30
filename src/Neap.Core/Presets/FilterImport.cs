using System.Globalization;
using System.Text.RegularExpressions;

namespace Neap.Core.Presets;

/// <summary>Why a line of a filter file was not used.</summary>
public enum SkipReason
{
    NotUnderstood,
    SwitchedOff,
    TypeNotOnHeadset,
    PastTenth,
}

/// <summary>A line of a filter file that was not used.</summary>
/// <param name="Line">The line as it was, trimmed.</param>
/// <param name="Reason">Why it was not used.</param>
/// <param name="Type">The filter type the line asked for, in capitals; empty when the line was not understood.</param>
public sealed record SkippedLine(string Line, SkipReason Reason, string Type);

/// <summary>A gain past the headset's limit, and the gain it was held to.</summary>
/// <param name="From">The gain the file asked for, in dB.</param>
/// <param name="To">The gain used, in dB.</param>
public sealed record HeldGain(double From, double To);

/// <summary>What reading a file of filters came to.</summary>
/// <param name="Bands">The filters that fit, in the order they came, at most ten.</param>
/// <param name="NotImported">The lines that were not used, each with its reason.</param>
/// <param name="HeldGains">The gains past the limit, each held to it.</param>
/// <param name="PreampDb">The preamp the file asked for, or null when it gave none.</param>
public sealed record FilterImportResult(
    IReadOnlyList<ParametricBand> Bands, IReadOnlyList<SkippedLine> NotImported, IReadOnlyList<HeldGain> HeldGains, double? PreampDb)
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
/// the person, since the headset's level is Pre-gain's to set.
/// </remarks>
public static partial class FilterImport
{
    /// <summary>The most text read from a filter file; a longer file is not one.</summary>
    public const int MostCharacters = 65536;

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
        var skipped = new List<SkippedLine>();
        var held = new List<HeldGain>();
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
                skipped.Add(new SkippedLine(line, SkipReason.NotUnderstood, string.Empty));
                continue;
            }

            string written = match.Groups[2].Value.ToUpperInvariant();
            if (!match.Groups[1].Value.Equals("ON", StringComparison.OrdinalIgnoreCase)) { skipped.Add(new SkippedLine(line, SkipReason.SwitchedOff, written)); continue; }
            if (!Types.TryGetValue(written, out var type)) { skipped.Add(new SkippedLine(line, SkipReason.TypeNotOnHeadset, written)); continue; }
            if (bands.Count >= BandWrites.Bands) { skipped.Add(new SkippedLine(line, SkipReason.PastTenth, written)); continue; }

            double gain = match.Groups[4].Success ? Number(match.Groups[4].Value) : 0;
            if (Math.Abs(gain) > ParametricBand.MostGain) held.Add(new HeldGain(gain, Math.Sign(gain) * ParametricBand.MostGain));
            double q = match.Groups[5].Success ? Number(match.Groups[5].Value) : DefaultQ(type);
            bands.Add(new ParametricBand(type, Number(match.Groups[3].Value), q, gain, true).Held());
        }

        return new FilterImportResult(bands, skipped, held, preamp);
    }

    /// <summary>The width a filter takes when the file gives none: a bell and a notch an octave, the rest the flat one.</summary>
    private static double DefaultQ(BandType type) => type is BandType.Peaking or BandType.Notch ? StockBands.Q : 0.7071;

    private static double Number(string text) => double.Parse(text, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^preamp:\s*(-?[0-9.]+)\s*db", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PreampLine();

    [GeneratedRegex(@"^filter\s*\d*:\s*(on|off)\s+([a-z]+)\s+fc\s+([0-9.]+)\s*hz(?:\s+gain\s+(-?[0-9.]+)\s*db)?(?:\s+q\s+([0-9.]+))?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FilterLine();
}
