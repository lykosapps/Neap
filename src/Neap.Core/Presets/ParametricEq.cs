namespace Neap.Core.Presets;

/// <summary>
/// One parametric adjustment: a boost or cut centred on a frequency, over a
/// width.
/// </summary>
/// <param name="Frequency">The centre, in hertz.</param>
/// <param name="Gain">The boost or cut at the centre, in tenths of a decibel.</param>
/// <param name="Width">The width, in tenths of an octave.</param>
public readonly record struct Adjustment(int Frequency, int Gain, int Width)
{
    public const int LowestFrequency = 20;
    public const int HighestFrequency = 20000;
    public const int MostGain = 90;

    /// <summary>The narrowest width, one octave: the width of the headset's own bands.</summary>
    /// <remarks>
    /// The bands cannot draw anything narrower than themselves, so a width
    /// below this would only ever be missed.
    /// </remarks>
    public const int Narrowest = 10;

    public const int Widest = 30;

    /// <summary>The same adjustment with every value inside its range.</summary>
    public Adjustment Held() => new(
        Math.Clamp(Frequency, LowestFrequency, HighestFrequency),
        Math.Clamp(Gain, -MostGain, MostGain),
        Math.Clamp(Width, Narrowest, Widest));
}

/// <summary>A parametric curve as it was made: its adjustments and the ten band gains they came to.</summary>
/// <remarks>
/// The gains are kept rather than fitted again, so the adjustments are
/// recognised by what the headset holds whatever the fit does since.
/// </remarks>
/// <param name="Adjustments">The adjustments.</param>
/// <param name="Bands">The ten band gains, in tenths, that played them.</param>
public sealed record ParametricShape(IReadOnlyList<Adjustment> Adjustments, IReadOnlyList<int> Bands)
{
    /// <summary>Whether these band gains are the ones this shape came to, so the adjustments still describe them.</summary>
    public bool Describes(IReadOnlyList<int> bands) => Bands.SequenceEqual(bands);
}

/// <summary>Where the parametric equaliser starts from when it is chosen.</summary>
public enum ParametricStart
{
    /// <summary>The adjustments set aside when the bands were chosen, untouched since.</summary>
    Resume,

    /// <summary>The preset's own stored adjustments, by choosing the preset again.</summary>
    Reopen,

    /// <summary>Flat: the curve has no adjustments that describe it.</summary>
    Flat,
}

/// <summary>
/// The parametric equaliser's maths: what a set of adjustments asks for, what
/// the headset's ten bands play, and the ten gains that come closest.
/// </summary>
/// <remarks>
/// <para>
/// The headset's game bank is ten peaking filters at fixed centres, each an
/// octave wide (Q 1.414) at 48 kHz, read from the filter table in the
/// headset's own configuration file. Only their gains can be written, so a
/// parametric adjustment is carried out by choosing the ten gains, never by
/// moving a band.
/// </para>
/// <para>
/// Neighbouring bands overlap, so what plays is the sum of all ten filters,
/// not a line through the ten gains. Everything here is worked out from the
/// filters themselves, so the curve shown is the curve heard.
/// </para>
/// </remarks>
public static class ParametricEq
{
    /// <summary>The most adjustments one curve can have.</summary>
    public const int MostAdjustments = 4;

    /// <summary>How far, in decibels, the headset can miss before it is worth saying.</summary>
    public const double AudibleMiss = 2.0;

    /// <summary>The headset's band centres, in hertz.</summary>
    public static readonly IReadOnlyList<double> Centres =
        [31.5, 63, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    /// <summary>Whether a bank can be shaped parametrically.</summary>
    /// <remarks>
    /// Only the game bank: the microphone's bands are a different set of
    /// filters, two of whose frequencies are inferred rather than read.
    /// </remarks>
    public static bool Covers(Bank bank) => bank == Bank.Game;

    private const double BandQ = 1.414;
    private const double SampleRate = 48000;
    private const double BandLimit = 9.0;

    /// <summary>
    /// The frequencies the fit is judged at: twelve to the octave, across the
    /// span the bands can reach.
    /// </summary>
    /// <remarks>
    /// Judging only at the ten centres ignores the dips and humps between
    /// them, which is where a fit misses most.
    /// </remarks>
    private static readonly double[] Judged =
        Enumerable.Range(0, 121).Select(i => 25 * Math.Pow(18000.0 / 25, i / 120.0)).ToArray();

    private const int Rounds = 8;

    /// <summary>What one peaking filter does at a frequency, in decibels.</summary>
    /// <remarks>The Audio EQ Cookbook peaking filter, the headset's own filter type.</remarks>
    public static double Peak(double centre, double gain, double q, double frequency)
    {
        if (gain == 0) return 0;
        double a = Math.Pow(10, gain / 40);
        double w0 = 2 * Math.PI * centre / SampleRate;
        double alpha = Math.Sin(w0) / (2 * q);
        double cos = Math.Cos(w0);
        double b0 = 1 + alpha * a, b1 = -2 * cos, b2 = 1 - alpha * a;
        double a0 = 1 + alpha / a, a1 = -2 * cos, a2 = 1 - alpha / a;

        double w = 2 * Math.PI * frequency / SampleRate;
        double c1 = Math.Cos(w), s1 = -Math.Sin(w), c2 = Math.Cos(2 * w), s2 = -Math.Sin(2 * w);
        double nr = b0 + b1 * c1 + b2 * c2, ni = b1 * s1 + b2 * s2;
        double dr = a0 + a1 * c1 + a2 * c2, di = a1 * s1 + a2 * s2;
        return 10 * Math.Log10((nr * nr + ni * ni) / (dr * dr + di * di));
    }

    /// <summary>The Q of a width given in tenths of an octave.</summary>
    public static double Q(int width)
    {
        double span = Math.Pow(2, width / 10.0);
        return Math.Sqrt(span) / (span - 1);
    }

    /// <summary>What the headset plays at a frequency, in decibels, for ten band gains in tenths.</summary>
    public static double Heard(IReadOnlyList<int> bands, double frequency)
    {
        double sum = 0;
        for (int i = 0; i < Centres.Count && i < bands.Count; i++)
            sum += Peak(Centres[i], bands[i] / 10.0, BandQ, frequency);
        return sum;
    }

    /// <summary>What the adjustments ask for at a frequency, in decibels.</summary>
    public static double Asked(IReadOnlyList<Adjustment> adjustments, double frequency)
    {
        double sum = 0;
        foreach (var adjustment in adjustments)
            sum += Peak(adjustment.Frequency, adjustment.Gain / 10.0, Q(adjustment.Width), frequency);
        return sum;
    }

    /// <summary>The ten band gains, in tenths, that come closest to what the adjustments ask for.</summary>
    /// <remarks>
    /// <para>
    /// A least-squares fit across <see cref="Judged"/>, by Gauss–Newton: a
    /// filter's effect in decibels is nearly, but not quite, proportional to
    /// its gain, so a few rounds of straight-line steps settle it. A band
    /// that would pass its limit is held at the limit and the rest are fitted
    /// again without it.
    /// </para>
    /// <para>
    /// Each gain is rounded to the headset's half-decibel steps on its own;
    /// see <see cref="PresetStore.BandStep"/>. Searching the steps for a
    /// slightly closer set moves bands far from any adjustment back and forth
    /// by a step as a point is dragged, for a gain of a quarter of a decibel
    /// at most.
    /// </para>
    /// </remarks>
    public static int[] Fit(IReadOnlyList<Adjustment> adjustments)
    {
        int count = Centres.Count;
        var gains = new double[count];
        if (adjustments.Count == 0) return new int[count];

        var asked = Judged.Select(f => Asked(adjustments, f)).ToArray();
        for (int round = 0; round < Rounds; round++)
        {
            var free = Enumerable.Range(0, count).ToList();
            while (free.Count > 0)
            {
                var step = Step(gains, asked, free);
                var over = free.Where((band, i) => Math.Abs(gains[band] + step[i]) > BandLimit).ToList();
                if (over.Count == 0)
                {
                    for (int i = 0; i < free.Count; i++) gains[free[i]] += step[i];
                    break;
                }
                foreach (int band in over)
                {
                    gains[band] = Math.Clamp(gains[band] + step[free.IndexOf(band)], -BandLimit, BandLimit);
                }
                free.RemoveAll(over.Contains);
            }
        }
        return gains.Select(g => PresetStore.Snap((int)Math.Round(g * 10))).ToArray();
    }

    /// <summary>One Gauss–Newton step for the free bands, the others held where they are.</summary>
    private static double[] Step(double[] gains, double[] asked, List<int> free)
    {
        const double nudge = 0.1, damping = 1e-3;
        int n = free.Count;

        var residual = new double[Judged.Length];
        for (int k = 0; k < Judged.Length; k++)
        {
            double heard = 0;
            for (int band = 0; band < gains.Length; band++)
                heard += Peak(Centres[band], gains[band], BandQ, Judged[k]);
            residual[k] = asked[k] - heard;
        }

        // Each band's effect is its own filter alone, so its slope needs
        // only that filter, nudged.
        var slope = new double[n][];
        for (int i = 0; i < n; i++)
        {
            double centre = Centres[free[i]], gain = gains[free[i]];
            slope[i] = Judged.Select(f =>
                (Peak(centre, gain + nudge, BandQ, f) - Peak(centre, gain, BandQ, f)) / nudge).ToArray();
        }

        var matrix = new double[n, n];
        var target = new double[n];
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                double sum = 0;
                for (int k = 0; k < Judged.Length; k++) sum += slope[i][k] * slope[j][k];
                matrix[i, j] = sum + (i == j ? damping : 0);
            }
            double pull = 0;
            for (int k = 0; k < Judged.Length; k++) pull += slope[i][k] * residual[k];
            target[i] = pull;
        }
        return Solve(matrix, target);
    }

    /// <summary>Solves a small linear system by Gaussian elimination with partial pivoting.</summary>
    private static double[] Solve(double[,] matrix, double[] target)
    {
        int n = target.Length;
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int row = col + 1; row < n; row++)
                if (Math.Abs(matrix[row, col]) > Math.Abs(matrix[pivot, col])) pivot = row;
            if (pivot != col)
            {
                for (int c = 0; c < n; c++) (matrix[col, c], matrix[pivot, c]) = (matrix[pivot, c], matrix[col, c]);
                (target[col], target[pivot]) = (target[pivot], target[col]);
            }
            for (int row = col + 1; row < n; row++)
            {
                double factor = matrix[row, col] / matrix[col, col];
                for (int c = col; c < n; c++) matrix[row, c] -= factor * matrix[col, c];
                target[row] -= factor * target[col];
            }
        }
        var result = new double[n];
        for (int row = n - 1; row >= 0; row--)
        {
            double sum = target[row];
            for (int c = row + 1; c < n; c++) sum -= matrix[row, c] * result[c];
            result[row] = sum / matrix[row, row];
        }
        return result;
    }

    /// <summary>The furthest, in decibels, the headset's curve lands from the one asked for.</summary>
    public static double Miss(IReadOnlyList<Adjustment> adjustments, IReadOnlyList<int> bands) =>
        Judged.Max(f => Math.Abs(Asked(adjustments, f) - Heard(bands, f)));

    /// <summary>Whether the headset misses the curve asked for by enough to hear, and so enough to say.</summary>
    public static bool FallsShort(IReadOnlyList<Adjustment> adjustments, IReadOnlyList<int> bands) =>
        adjustments.Count > 0 && Miss(adjustments, bands) > AudibleMiss;

    /// <summary>Where a new adjustment starts: flat, so adding one changes nothing until it is moved.</summary>
    /// <remarks>
    /// It goes to the first of a few spread-out frequencies that is more
    /// than an octave from any adjustment already there, so a second one
    /// does not land on top of the first.
    /// </remarks>
    public static Adjustment Next(IReadOnlyList<Adjustment> existing)
    {
        int[] places = [1000, 4000, 250, 63, 12000];
        int frequency = places.FirstOrDefault(
            place => existing.All(a => Math.Abs(Math.Log2((double)place / a.Frequency)) > 1), places[0]);
        return new Adjustment(frequency, 0, 15);
    }

    /// <summary>Decides where the parametric equaliser starts from when it is chosen.</summary>
    /// <remarks>
    /// Adjustments set aside by a look at the bands come back while the bands
    /// are still where they left them. Otherwise a preset made parametrically
    /// comes back in that form, unless its gains have been changed elsewhere
    /// since, and anything else starts flat.
    /// </remarks>
    /// <param name="setAside">The shape in use when the bands were chosen, if any.</param>
    /// <param name="live">The bands as they are now.</param>
    /// <param name="stored">The baseline preset's stored shape, if any.</param>
    /// <param name="preset">The baseline preset's bands, if there is one.</param>
    public static ParametricStart StartFrom(ParametricShape? setAside, IReadOnlyList<int> live,
        ParametricShape? stored, IReadOnlyList<int>? preset)
    {
        if (setAside is not null && setAside.Describes(live)) return ParametricStart.Resume;
        if (stored is not null && preset is not null && stored.Describes(preset)) return ParametricStart.Reopen;
        return ParametricStart.Flat;
    }

    /// <summary>Whether starting from here changes what is heard, so the person is asked first.</summary>
    /// <remarks>
    /// Starting flat changes a curve that is not flat already, and reopening a
    /// preset changes a curve moved away from it. Resuming puts back the
    /// adjustments that made the curve as it is, so it changes nothing.
    /// </remarks>
    /// <param name="start">Where it would start, from <see cref="StartFrom"/>.</param>
    /// <param name="live">The bands as they are now.</param>
    /// <param name="preset">The baseline preset's bands, if there is one.</param>
    public static bool StartChangesSound(ParametricStart start, IReadOnlyList<int> live, IReadOnlyList<int>? preset) =>
        start switch
        {
            ParametricStart.Flat => live.Any(band => band != 0),
            ParametricStart.Reopen => preset is not null && !live.SequenceEqual(preset),
            _ => false,
        };

    // -- the plot ----------------------------------------------------------

    /// <summary>Where a frequency sits across a plot of this width, on a logarithmic scale.</summary>
    public static double X(double frequency, double width) =>
        width * Math.Log(frequency / Adjustment.LowestFrequency)
              / Math.Log((double)Adjustment.HighestFrequency / Adjustment.LowestFrequency);

    /// <summary>
    /// Where a dragged adjustment goes: moved by how far the pointer has gone
    /// since the press, from where it was, with its gain in the headset's steps.
    /// </summary>
    /// <remarks>
    /// Moved by the distance, not put under the pointer: a press lands
    /// anywhere within reach of a point, so putting it under the pointer
    /// would throw it off its value the moment it was pressed. A movement too
    /// small to mean a drag leaves it alone.
    /// </remarks>
    /// <param name="start">The adjustment as it was when pressed.</param>
    /// <param name="across">Pixels moved to the right since the press.</param>
    /// <param name="down">Pixels moved down since the press.</param>
    /// <param name="width">The plot's width, in pixels.</param>
    /// <param name="height">The plot's height, in pixels.</param>
    /// <param name="range">The plot's range either side of 0 dB, in tenths.</param>
    public static Adjustment Dragged(Adjustment start, double across, double down,
        double width, double height, int range)
    {
        if (Math.Sqrt(across * across + down * down) < ResponseCurve.DragThreshold || width <= 0 || height <= 0)
            return start;
        int frequency = FrequencyAt(X(start.Frequency, width) + across, width);
        int gain = PresetStore.Snap((int)Math.Round(start.Gain - down * 2 * range / height));
        return (start with { Frequency = frequency, Gain = gain }).Held();
    }

    /// <summary>
    /// The frequency at a point across a plot of this width, held inside the
    /// range and rounded to three significant figures.
    /// </summary>
    /// <remarks>
    /// A pixel is worth about 30 Hz at the top of the range, so a dragged
    /// frequency to the hertz only looks precise.
    /// </remarks>
    public static int FrequencyAt(double x, double width)
    {
        double t = width <= 0 ? 0 : Math.Clamp(x / width, 0, 1);
        double frequency = Adjustment.LowestFrequency
            * Math.Pow((double)Adjustment.HighestFrequency / Adjustment.LowestFrequency, t);
        double step = Math.Pow(10, Math.Floor(Math.Log10(frequency)) - 2);
        return (int)(Math.Round(frequency / step) * step);
    }
}
