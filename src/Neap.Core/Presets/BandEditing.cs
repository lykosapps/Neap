namespace Neap.Core.Presets;

/// <summary>Where frequency and gain sit on the parametric curve's plot, and which point a press lands on.</summary>
/// <remarks>
/// Frequency runs left to right on a logarithmic scale from 20 Hz to 20 kHz.
/// The plot shows 14 dB above and below zero, and a band's gain stops at 12.
/// </remarks>
/// <param name="Width">The plot's width.</param>
/// <param name="Height">The plot's height.</param>
/// <param name="ShownDb">The decibels shown above and below zero: <see cref="CloseDb"/> for the bands alone, <see cref="WideDb"/> with a measurement or target.</param>
public readonly record struct PlotScale(double Width, double Height, double ShownDb = PlotScale.CloseDb)
{
    /// <summary>The decibels shown above and below zero for the bands alone, just past their 12 dB.</summary>
    public const double CloseDb = 14;

    /// <summary>The decibels shown above and below zero with a measurement or target, which reach further than a band.</summary>
    public const double WideDb = 20;

    private const double LowestHertz = 20;
    private const double HighestHertz = 20000;

    /// <summary>The space kept clear above and below the plot's extremes.</summary>
    private const double Margin = 8;

    private double PerDb => (Height / 2 - Margin) / ShownDb;

    /// <summary>The distance from the left edge of a frequency.</summary>
    public double X(double hertz) => Width * LogScale.Share(hertz, LowestHertz, HighestHertz);

    /// <summary>The frequency at a distance from the left edge, held to the plot.</summary>
    public double Hertz(double x) => LogScale.At(x / Width, LowestHertz, HighestHertz);

    /// <summary>The distance from the top of a gain, which a line or point never leaves the plot's extremes from.</summary>
    public double Y(double db) => Height / 2 - (Math.Clamp(db, -ShownDb, ShownDb) * PerDb);

    /// <summary>The gain at a distance from the top, held to what a band may be set to.</summary>
    public double Gain(double y) => Math.Clamp((Height / 2 - y) / PerDb, -ParametricBand.MostGain, ParametricBand.MostGain);

    /// <summary>The band in use whose point is nearest a press, within a distance, or -1 when none is.</summary>
    /// <remarks>A band not in use is not drawn, so it cannot be pressed.</remarks>
    /// <param name="bands">The bands.</param>
    /// <param name="x">The press's distance from the left edge.</param>
    /// <param name="y">The press's distance from the top.</param>
    /// <param name="within">How near a point the press must be.</param>
    public int Nearest(IReadOnlyList<ParametricBand> bands, double x, double y, double within)
    {
        int found = -1;
        double nearest = within;
        for (int band = 0; band < bands.Count; band++)
        {
            double dx = X(bands[band].Hertz) - x, dy = Y(PointDb(bands[band])) - y;
            double distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (!bands[band].InUse || distance > nearest) continue;
            nearest = distance;
            found = band;
        }

        return found;
    }

    /// <summary>The gain a band's point is drawn at: its own when it has one in use, else zero.</summary>
    /// <param name="band">The band.</param>
    public static double PointDb(ParametricBand band) => band.InUse && band.UsesGain ? band.Gain : 0;
}

/// <summary>The changes a person makes to a band: dragging, widening, nudging, choosing a filter.</summary>
/// <remarks>
/// Every result is held inside the band's limits. Fine movement, with Shift,
/// is a fifth of the usual.
/// </remarks>
public static class BandEditing
{
    private const double Fine = 0.2;

    /// <summary>The width a filter starts at when it is chosen: a bell and a notch an octave, the others the flat one.</summary>
    /// <param name="type">The filter.</param>
    public static double StartingQ(BandType type) => type is BandType.Peaking or BandType.Notch ? StockBands.Q : 0.7071;

    /// <summary>The band with another filter, in use and at that filter's starting width.</summary>
    /// <param name="band">The band.</param>
    /// <param name="type">The filter.</param>
    public static ParametricBand WithType(ParametricBand band, BandType type) =>
        (band with { Type = type, Q = StartingQ(type), InUse = true }).Held();

    /// <summary>The band put where a drag took its point: the frequency, and the gain too when it has one.</summary>
    /// <param name="start">The band when the drag began.</param>
    /// <param name="scale">The plot.</param>
    /// <param name="fromX">Where the press began, from the left.</param>
    /// <param name="fromY">Where the press began, from the top.</param>
    /// <param name="toX">Where the pointer is now, from the left.</param>
    /// <param name="toY">Where the pointer is now, from the top.</param>
    /// <param name="fine">Whether Shift is held.</param>
    public static ParametricBand Dragged(ParametricBand start, PlotScale scale, double fromX, double fromY, double toX, double toY, bool fine)
    {
        double factor = fine ? Fine : 1;
        double x = scale.X(start.Hertz) + ((toX - fromX) * factor);
        var moved = start with { Hertz = scale.Hertz(x), InUse = true };
        if (!start.UsesGain) return moved.Held();
        double y = scale.Y(PlotScale.PointDb(start)) + ((toY - fromY) * factor);
        return (moved with { Gain = Math.Round(scale.Gain(y) * 10) / 10 }).Held();
    }

    /// <summary>The band wider or narrower by wheel ticks, a tenth wider for each up and a tenth narrower for each down.</summary>
    /// <param name="band">The band.</param>
    /// <param name="ticks">Wheel ticks, up positive.</param>
    /// <param name="fine">Whether Shift is held.</param>
    public static ParametricBand Widened(ParametricBand band, int ticks, bool fine) =>
        (band with { Q = band.Q * Math.Pow(1 / 1.1, ticks * (fine ? Fine : 1)), InUse = true }).Held();

    /// <summary>The band moved by the arrow keys: a semitone across, half a decibel up or down.</summary>
    /// <param name="band">The band.</param>
    /// <param name="across">Presses of right, positive, or left.</param>
    /// <param name="up">Presses of up, positive, or down.</param>
    /// <param name="fine">Whether Shift is held.</param>
    public static ParametricBand Nudged(ParametricBand band, int across, int up, bool fine)
    {
        double factor = fine ? Fine : 1;
        var moved = band with { Hertz = band.Hertz * Math.Pow(2, across * factor / 12), InUse = true };
        return (band.UsesGain ? moved with { Gain = Math.Round(((band.Gain) + (up * 0.5 * factor)) * 10) / 10 } : moved).Held();
    }

    /// <summary>The first band not in use, or -1 when all ten are.</summary>
    /// <param name="bands">The bands.</param>
    public static int FirstFree(IReadOnlyList<ParametricBand> bands)
    {
        for (int band = 0; band < bands.Count; band++)
            if (!bands[band].InUse) return band;
        return -1;
    }

    /// <summary>A bell at a place on the plot, as a double press on empty space makes.</summary>
    /// <param name="scale">The plot.</param>
    /// <param name="x">From the left.</param>
    /// <param name="y">From the top.</param>
    public static ParametricBand BellAt(PlotScale scale, double x, double y) =>
        (new ParametricBand(BandType.Peaking, scale.Hertz(x), StockBands.Q, Math.Round(scale.Gain(y) * 10) / 10, true)).Held();

    /// <summary>The bands in use, in order of frequency, then the rest.</summary>
    /// <remarks>
    /// The headset plays its ten filters one after another, and filters of
    /// this kind sound the same in any order, so ordering them changes
    /// nothing heard. It keeps the numbers on the curve and in the table
    /// rising from left to right whenever a whole shape is put on, or a band
    /// added or removed. A band being dragged keeps its number until then.
    /// Bands at the same frequency keep their order.
    /// </remarks>
    /// <param name="bands">The ten bands.</param>
    public static IReadOnlyList<ParametricBand> InOrder(IReadOnlyList<ParametricBand> bands) =>
        [.. bands.Where(band => band.InUse).OrderBy(band => band.Hertz), .. bands.Where(band => !band.InUse)];

    /// <summary>The bands with one more in use, in order, and where it went; or null when all ten are.</summary>
    /// <param name="bands">The ten bands.</param>
    /// <param name="band">The band to add.</param>
    public static (IReadOnlyList<ParametricBand> Bands, int At)? Added(IReadOnlyList<ParametricBand> bands, ParametricBand band)
    {
        int free = FirstFree(bands);
        if (free < 0) return null;
        var placed = bands.Select((b, i) => (Band: i == free ? band.Held() with { InUse = true } : b, New: i == free)).ToList();
        var ordered = placed.Where(p => p.Band.InUse).OrderBy(p => p.Band.Hertz).Concat(placed.Where(p => !p.Band.InUse)).ToList();
        return ([.. ordered.Select(p => p.Band)], ordered.FindIndex(p => p.New));
    }

    /// <summary>The bands with one taken out of use, in order, and which band to choose next: the one now in its place, or else the last.</summary>
    /// <param name="bands">The ten bands.</param>
    /// <param name="band">Which band to remove, counted from zero.</param>
    public static (IReadOnlyList<ParametricBand> Bands, int Chosen) Removed(IReadOnlyList<ParametricBand> bands, int band)
    {
        var left = InOrder([.. bands.Select((b, i) => i == band ? b with { InUse = false } : b)]);
        int inUse = left.Count(b => b.InUse);
        return (left, Math.Max(0, Math.Min(band, inUse - 1)));
    }

    /// <summary>A bell at no gain for a band added by its button: in the middle of the widest gap the bands in use leave.</summary>
    /// <remarks>The gaps are measured on the plot's scale, octaves, from 20 Hz to the highest frequency a band may have.</remarks>
    /// <param name="bands">The ten bands.</param>
    public static ParametricBand NewBand(IReadOnlyList<ParametricBand> bands)
    {
        double[] edges = [20, .. bands.Where(b => b.InUse).Select(b => b.Hertz).Order(), ParametricBand.HighestHertz];
        int widest = 0;
        for (int i = 1; i < edges.Length - 1; i++)
            if (Math.Log(edges[i + 1] / edges[i]) > Math.Log(edges[widest + 1] / edges[widest])) widest = i;
        double hertz = Math.Sqrt(edges[widest] * edges[widest + 1]);
        return new ParametricBand(BandType.Peaking, hertz, StockBands.Q, 0, true).Held();
    }
}
