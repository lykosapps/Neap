namespace Neap.Core.Presets;

/// <summary>What following another app's gains came to.</summary>
/// <param name="Bands">The ten bands after following, with their own filter, frequency and width kept.</param>
/// <param name="ChangedBands">The bands whose gain was taken from the headset, counted from zero.</param>
public sealed record GainFollowResult(IReadOnlyList<ParametricBand> Bands, IReadOnlyList<int> ChangedBands)
{
    /// <summary>Whether another app changed any gain that is heard.</summary>
    public bool Changed => ChangedBands.Count > 0;
}

/// <summary>
/// The rule for another app, such as Swarm II, changing the equaliser's gains:
/// the bands keep their filter, frequency and width, and take the new gains.
/// </summary>
/// <remarks>
/// Swarm II writes only gains, set for its own fixed bands. Neap cannot stop
/// it while closed, so one rule holds either way: the last change wins, and
/// what the headset reports is what Neap shows. A band that is off, or whose
/// filter has no gain, is not heard to change, so it is left alone.
/// </remarks>
public static class GainFollow
{
    /// <summary>The smallest difference in decibels that counts as a change.</summary>
    public const double Tolerance = 0.05;

    /// <summary>The bands as they stand once the headset's reported gains are taken into account.</summary>
    /// <param name="mine">The ten bands Neap holds.</param>
    /// <param name="lastSeen">The ten gains the headset reported when Neap last looked, or wrote.</param>
    /// <param name="reported">The ten gains the headset reports now.</param>
    public static GainFollowResult Follow(
        IReadOnlyList<ParametricBand> mine, IReadOnlyList<double> lastSeen, IReadOnlyList<double> reported)
    {
        if (mine.Count != BandWrites.Bands) throw new ArgumentException($"expected {BandWrites.Bands} bands, got {mine.Count}", nameof(mine));
        if (lastSeen.Count != BandWrites.Bands) throw new ArgumentException($"expected {BandWrites.Bands} gains, got {lastSeen.Count}", nameof(lastSeen));
        if (reported.Count != BandWrites.Bands) throw new ArgumentException($"expected {BandWrites.Bands} gains, got {reported.Count}", nameof(reported));

        var bands = mine.ToArray();
        var changed = new List<int>();
        for (int band = 0; band < bands.Length; band++)
        {
            if (Math.Abs(reported[band] - lastSeen[band]) <= Tolerance) continue;
            if (!bands[band].InUse || !bands[band].UsesGain) continue;
            bands[band] = bands[band] with { Gain = Math.Clamp(reported[band], -ParametricBand.MostGain, ParametricBand.MostGain) };
            changed.Add(band);
        }

        return new GainFollowResult(bands, changed);
    }
}
