using System.Globalization;

namespace Neap.Core.Presets;

/// <summary>Reads a frequency typed into the parametric equaliser.</summary>
public static class FrequencyEntry
{
    /// <summary>The frequency in hertz, or null if the text holds no number.</summary>
    /// <remarks>
    /// <para>
    /// "4.9k", "4.9 kHz" and "4900" all mean 4.9 kHz. A bare number below the
    /// bottom of the range is taken as kilohertz: the field shows kilohertz
    /// for anything above 1 kHz, so "16" typed over "15.8 kHz" means 16 kHz,
    /// not a frequency too low to set.
    /// </para>
    /// <para>
    /// A comma is read as a decimal point, as either is typed whatever the
    /// PC's language.
    /// </para>
    /// </remarks>
    public static double? Parse(string text)
    {
        string lower = text.ToLowerInvariant().Replace(',', '.');
        string number = new(lower.Where(c => char.IsDigit(c) || c == '.').ToArray());
        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return null;
        bool kilo = lower.Contains('k', StringComparison.Ordinal)
                    || (!lower.Contains("hz", StringComparison.Ordinal) && value < Adjustment.LowestFrequency);
        return kilo ? value * 1000 : value;
    }
}
