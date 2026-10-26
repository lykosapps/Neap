using System.Globalization;
using System.Text.RegularExpressions;

namespace Neap.Core.Presets;

/// <summary>Reads a frequency typed into the parametric equaliser.</summary>
public static partial class FrequencyEntry
{
    /// <summary>The frequency in hertz, or null if the text holds no number.</summary>
    /// <param name="text">What was typed.</param>
    /// <param name="shown">The frequency the field showed before, in hertz; 0 when none is known.</param>
    /// <remarks>
    /// <para>
    /// "4.9k", "4.9 kHz" and "4900" all mean 4.9 kHz. A bare number takes the
    /// unit the field showed: over "15.8 kHz" or "20 kHz", "16" or "20" means
    /// kilohertz, and anything above 20 is hertz, as kilohertz would be past
    /// the top of the range. With no frequency
    /// shown, a bare number below the bottom of the range is taken as
    /// kilohertz, as it could not be a frequency to set.
    /// </para>
    /// <para>
    /// A comma between groups of three digits, as in "1,500 Hz", is a
    /// thousands separator; any other comma is a decimal point, as either is
    /// typed whatever the PC's language.
    /// </para>
    /// </remarks>
    public static double? Parse(string text, double shown = 0)
    {
        string lower = text.ToLowerInvariant();
        string number = new(lower.Where(c => char.IsDigit(c) || c == '.' || c == ',').ToArray());
        number = ThousandsComma().IsMatch(number) ? number.Replace(",", "", StringComparison.Ordinal) : number.Replace(',', '.');
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return null;
        bool unitTyped = lower.Contains('k', StringComparison.Ordinal) || lower.Contains("hz", StringComparison.Ordinal);
        bool kilo = lower.Contains('k', StringComparison.Ordinal)
                    || (!unitTyped && (shown >= 1000 ? value * 1000 <= Adjustment.HighestFrequency : value < Adjustment.LowestFrequency));
        return kilo ? value * 1000 : value;
    }

    [GeneratedRegex(@"^\d{1,3}(,\d{3})+(\.\d+)?$")]
    private static partial Regex ThousandsComma();
}
