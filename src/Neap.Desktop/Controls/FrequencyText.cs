using System.Globalization;

namespace Neap.Desktop.Controls;

/// <summary>Shows a frequency for the parametric equaliser: hertz below 1 kHz, kilohertz above.</summary>
/// <remarks>Reading one typed is <see cref="Neap.Core.Presets.FrequencyEntry.Parse"/>.</remarks>
internal static class FrequencyText
{
    public static string Of(double hertz) => hertz >= 1000
        ? Strings.Format("Parametric_KiloHertz", (hertz / 1000).ToString("0.##", CultureInfo.InvariantCulture))
        : Strings.Format("Parametric_Hertz", Math.Round(hertz).ToString(CultureInfo.InvariantCulture));
}
