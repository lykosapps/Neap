using System.Globalization;

namespace Neap.Core.Presets;

/// <summary>Formats a band value, held everywhere in tenths of a decibel, as signed decibels.</summary>
/// <remarks>
/// The sign makes a boost and a cut read as opposites ("+3.0" and "−3.0"), and
/// leaves zero bare.
/// </remarks>
public static class Db
{
    /// <summary>The value as text, such as "+4.5", "−12.0" or "0.0".</summary>
    public static string Text(int tenths)
    {
        string sign = tenths > 0 ? "+" : tenths < 0 ? "−" : "";
        return $"{sign}{Math.Abs(tenths / 10.0).ToString("0.0", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Reads a value a person typed, as decibels, or null if it is not one.</summary>
    /// <remarks>
    /// Lenient: "3", "3.5", "-3", "−3", "+3 dB" and "3,5" are all accepted, and
    /// anything else around the number is ignored.
    /// </remarks>
    public static double? Parse(string text)
    {
        string cleaned = new(text.Replace('−', '-').Replace(',', '.')
            .Where(c => char.IsDigit(c) || c is '.' or '-' or '+').ToArray());
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double db) ? db : null;
    }
}
