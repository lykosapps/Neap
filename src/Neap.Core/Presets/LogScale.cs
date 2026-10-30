namespace Neap.Core.Presets;

/// <summary>Where a value sits between two ends on a logarithmic scale, as the share of the way from the lower end.</summary>
/// <remarks>
/// Frequency is heard by ratio, so the curve's plot, the frequency dial and
/// the tone's slider all put each doubling the same distance apart. They share
/// this one scale so a band sits at the same place on each.
/// </remarks>
public static class LogScale
{
    /// <summary>The share of the way from <paramref name="lowest"/> to <paramref name="highest"/>, 0 to 1, held to the ends.</summary>
    public static double Share(double value, double lowest, double highest) =>
        Math.Log(Math.Clamp(value, lowest, highest) / lowest) / Math.Log(highest / lowest);

    /// <summary>The value a share of the way from <paramref name="lowest"/> to <paramref name="highest"/>, held to the ends.</summary>
    public static double At(double share, double lowest, double highest) =>
        Math.Clamp(lowest * Math.Pow(highest / lowest, Math.Clamp(share, 0, 1)), lowest, highest);
}
