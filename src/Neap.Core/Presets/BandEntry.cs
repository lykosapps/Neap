using System.Globalization;
using System.Text.RegularExpressions;

namespace Neap.Core.Presets;

/// <summary>Reads a width or a gain typed into a parametric band's field.</summary>
/// <remarks>
/// Either comma or full stop is a decimal point, as either is typed whatever the
/// PC's language. A frequency is <see cref="FrequencyEntry"/>'s. Each reading
/// is held inside the band's limits, and text that holds no number is null, so
/// the field puts back what it had.
/// </remarks>
public static partial class BandEntry
{
    /// <summary>The width typed, or null if the text holds no number.</summary>
    /// <param name="text">What was typed.</param>
    public static double? Q(string text) =>
        Number(text) is double value ? Math.Clamp(value, ParametricBand.LowestQ, ParametricBand.HighestQ) : null;

    /// <summary>The gain typed, in decibels, or null if the text holds no number.</summary>
    /// <param name="text">What was typed, such as "+3", "-2,5" or "4 dB".</param>
    public static double? Gain(string text) =>
        Number(text) is double value ? Math.Clamp(Math.Round(value * 10, MidpointRounding.AwayFromZero) / 10, -ParametricBand.MostGain, ParametricBand.MostGain) : null;

    private static double? Number(string text)
    {
        var match = Digits().Match(text.Replace(',', '.').Replace('−', '-'));
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ? value : null;
    }

    [GeneratedRegex(@"[-+−]?\s*\d*\.?\d+")]
    private static partial Regex Digits();
}
