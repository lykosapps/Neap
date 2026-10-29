namespace Neap.Core.Presets;

/// <summary>Finds the parametric bands that best make a wanted change to the sound, frequency by frequency.</summary>
/// <remarks>
/// <para>
/// The wanted change is read onto a grid of 24 points an octave from 20 Hz to
/// <see cref="HighestFitted"/>, interpolated on log frequency, smoothed over a
/// sixth of an octave so no band chases a measurement's ripples, and held
/// between a 12 dB cut and a <see cref="MostBoost"/> boost.
/// </para>
/// <para>
/// Bands are then added one at a time. Each new band starts where the error
/// left by the bands so far is largest: a bell as wide as the error's peak at
/// half its height, or a low or high shelf at its usual corner when the
/// error is a broad tilt at that end. Each start is tuned on its own, the one
/// that fits best is kept, and then every band's frequency, gain and width is
/// tuned again in turn, in steps that halve whenever no step helps, against
/// the root mean square error over the grid. Adding stops at the band limit,
/// when the largest error left is under half a decibel, or when a band would
/// improve the fit by less than <see cref="LeastImprovement"/>, so a nearly
/// flat change gets few bands or none. There is no randomness, so the same
/// change always gives the same bands.
/// </para>
/// <para>
/// Cuts are preferred to boosts: a boost uses headroom the headset's
/// compressor then squashes (see <see cref="LevelHeadroom"/>), so the wanted
/// change and every band's gain stop at <see cref="MostBoost"/>, and the
/// error counts the bands together rising past it a hundred times over, while
/// a cut may go to the full 12 dB. 6 dB keeps the larger part of a typical
/// correction and leaves the loudness level 6 dB of the 12 it could need to
/// lower. Bells are kept between a Q of 0.3 and
/// <see cref="MostQ"/>, and shelves between 0.5 and 1, where they rise
/// without a bump.
/// </para>
/// </remarks>
public static class AutoEq
{
    /// <summary>The highest frequency fitted.</summary>
    /// <remarks>
    /// Above it, a measurement changes with how the headset sits on the head,
    /// so it is left alone, as the AutoEq project does. No band is centred above it.
    /// </remarks>
    public const double HighestFitted = 10000;

    /// <summary>The largest boost the fit asks for, at any frequency and from any band.</summary>
    public const double MostBoost = 6;

    /// <summary>The narrowest a fitted bell may be.</summary>
    public const double MostQ = 6;

    /// <summary>The least a band must lower the root mean square error, in decibels, to be added.</summary>
    public const double LeastImprovement = 0.05;

    private const double LeastQ = 0.3;
    private const double LeastShelfQ = 0.5;
    private const double MostShelfQ = 1;
    private const double LeastError = 0.5;
    private const int PointsPerOctave = 24;
    private const int SmoothingPoints = PointsPerOctave / 12;
    private const double LowShelfHertz = 100;
    private const double HighShelfHertz = 5000;
    private const int HalvingsInTuning = 5;
    private const double OverBoostWeight = 100;

    /// <summary>The ten bands that best make the wanted change, the fitted ones in use and in order of frequency.</summary>
    /// <param name="wanted">The change wanted at each frequency, in decibels: target minus measured, aligned by the caller. Any order and spacing.</param>
    /// <param name="bands">The most bands to use.</param>
    public static IReadOnlyList<ParametricBand> Fit(IReadOnlyList<(double Hertz, double Db)> wanted, int bands = BandWrites.Bands)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bands);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bands, BandWrites.Bands);

        double[] grid = Grid();
        double[] target = Target(wanted, grid);
        var fit = new Fitting(grid, target);

        while (fit.Bands.Count < bands)
        {
            int peak = fit.LargestError();
            if (Math.Abs(fit.Error(peak)) < LeastError) break;

            double before = fit.Rms();
            var kept = fit.Copy();
            fit.Add(BestStart(fit, peak));
            fit.Tune(from: 0);
            if (before - fit.Rms() < LeastImprovement)
            {
                fit = kept;
                break;
            }
        }

        var fitted = fit.Bands.Select(Rounded).ToList();
        return BandEditing.InOrder([.. fitted, .. StockBands.Flat.Skip(fitted.Count)]);
    }

    /// <summary>The frequencies fitted: 24 an octave from 20 Hz to the highest fitted.</summary>
    private static double[] Grid()
    {
        int count = (int)Math.Floor(Math.Log2(HighestFitted / ParametricBand.LowestHertz) * PointsPerOctave) + 1;
        return [.. Enumerable.Range(0, count).Select(i => ParametricBand.LowestHertz * Math.Pow(2, (double)i / PointsPerOctave))];
    }

    /// <summary>The wanted change on the grid: interpolated on log frequency, smoothed, and held to the gains allowed.</summary>
    private static double[] Target(IReadOnlyList<(double Hertz, double Db)> wanted, double[] grid)
    {
        var points = wanted.Where(p => p.Hertz > 0 && double.IsFinite(p.Hertz) && double.IsFinite(p.Db)).OrderBy(p => p.Hertz).ToArray();
        if (points.Length == 0) return new double[grid.Length];

        double[] raw = [.. grid.Select(hertz => Interpolate(points, hertz))];
        var target = new double[grid.Length];
        for (int i = 0; i < grid.Length; i++)
        {
            int from = Math.Max(0, i - SmoothingPoints), to = Math.Min(grid.Length - 1, i + SmoothingPoints);
            double sum = 0;
            for (int j = from; j <= to; j++) sum += raw[j];
            target[i] = Math.Clamp(sum / (to - from + 1), -ParametricBand.MostGain, MostBoost);
        }

        return target;
    }

    /// <summary>The value at a frequency, along straight lines on log frequency, held at the ends.</summary>
    private static double Interpolate((double Hertz, double Db)[] points, double hertz)
    {
        if (hertz <= points[0].Hertz) return points[0].Db;
        if (hertz >= points[^1].Hertz) return points[^1].Db;

        int above = 1;
        while (points[above].Hertz < hertz) above++;
        var (low, high) = (points[above - 1], points[above]);
        if (high.Hertz == low.Hertz) return high.Db;
        double t = Math.Log(hertz / low.Hertz) / Math.Log(high.Hertz / low.Hertz);
        return low.Db + (t * (high.Db - low.Db));
    }

    /// <summary>The best of the starts for a new band, each tuned on its own: a bell at the peak, and a shelf at each end not yet used.</summary>
    private static ParametricBand BestStart(Fitting fit, int peak)
    {
        var starts = new List<ParametricBand> { Bell(fit, peak) };
        if (!fit.Bands.Any(b => b.Type == BandType.LowShelf)) starts.Add(Shelf(fit, BandType.LowShelf, LowShelfHertz));
        if (!fit.Bands.Any(b => b.Type == BandType.HighShelf)) starts.Add(Shelf(fit, BandType.HighShelf, HighShelfHertz));

        ParametricBand best = default;
        double bestRms = double.PositiveInfinity;
        foreach (var start in starts)
        {
            var trial = fit.Copy();
            trial.Add(start);
            trial.Tune(from: trial.Bands.Count - 1);
            if (trial.Rms() >= bestRms) continue;
            bestRms = trial.Rms();
            best = trial.Bands[^1];
        }

        return best;
    }

    /// <summary>A bell at the error's peak, as wide as the peak is at half its height.</summary>
    private static ParametricBand Bell(Fitting fit, int peak)
    {
        double height = fit.Error(peak);
        int left = peak, right = peak;
        while (left > 0 && fit.Error(left - 1) * height > height * height / 4) left--;
        while (right < fit.Grid.Length - 1 && fit.Error(right + 1) * height > height * height / 4) right++;

        double octaves = Math.Max(right - left, 2) / (double)PointsPerOctave;
        double ratio = Math.Pow(2, octaves);
        return Held(new ParametricBand(BandType.Peaking, fit.Grid[peak], Math.Sqrt(ratio) / (ratio - 1), height, true));
    }

    /// <summary>A shelf at its usual corner, with the mean error beyond the corner as its gain.</summary>
    private static ParametricBand Shelf(Fitting fit, BandType type, double hertz)
    {
        var beyond = Enumerable.Range(0, fit.Grid.Length).Where(i => type == BandType.LowShelf ? fit.Grid[i] <= hertz : fit.Grid[i] >= hertz);
        return Held(new ParametricBand(type, hertz, 0.7071, beyond.Average(fit.Error), true));
    }

    /// <summary>The band inside the limits the fit keeps to.</summary>
    private static ParametricBand Held(ParametricBand band) => band with
    {
        Hertz = Math.Clamp(band.Hertz, ParametricBand.LowestHertz, HighestFitted),
        Gain = Math.Clamp(band.Gain, -ParametricBand.MostGain, MostBoost),
        Q = band.Type == BandType.Peaking ? Math.Clamp(band.Q, LeastQ, MostQ) : Math.Clamp(band.Q, LeastShelfQ, MostShelfQ),
    };

    /// <summary>The band at the precision a person sets it to: whole hertz, hundredths of Q and tenths of a decibel.</summary>
    private static ParametricBand Rounded(ParametricBand band) => band with
    {
        Hertz = Math.Round(band.Hertz),
        Q = Math.Round(band.Q, 2),
        Gain = Math.Round(band.Gain, 1),
    };

    /// <summary>The bands fitted so far, what each does on the grid, and what they do together.</summary>
    private sealed class Fitting
    {
        private readonly double[] _target;
        private readonly List<double[]> _responses;
        private readonly double[] _total;

        public Fitting(double[] grid, double[] target)
            : this(grid, target, [], [], new double[grid.Length])
        {
        }

        private Fitting(double[] grid, double[] target, List<ParametricBand> bands, List<double[]> responses, double[] total)
        {
            Grid = grid;
            _target = target;
            Bands = bands;
            _responses = responses;
            _total = total;
        }

        public double[] Grid { get; }

        public List<ParametricBand> Bands { get; }

        public Fitting Copy() => new(Grid, _target, [.. Bands], [.. _responses], (double[])_total.Clone());

        /// <summary>What is still wanted at a grid point once the bands so far are applied.</summary>
        public double Error(int i) => _target[i] - _total[i];

        /// <summary>The grid point with the largest error; the lowest frequency wins a tie.</summary>
        public int LargestError()
        {
            int largest = 0;
            for (int i = 1; i < Grid.Length; i++)
            {
                if (Math.Abs(Error(i)) > Math.Abs(Error(largest))) largest = i;
            }

            return largest;
        }

        public double Rms() => Rms(_total);

        public void Add(ParametricBand band)
        {
            double[] response = Response(band);
            Bands.Add(band);
            _responses.Add(response);
            for (int i = 0; i < Grid.Length; i++) _total[i] += response[i];
        }

        /// <summary>Tunes the frequency, gain and width of the bands from one onwards, in turn, halving the steps whenever no step helps.</summary>
        public void Tune(int from)
        {
            double octaves = 1.0 / 6, db = 1, widths = 0.25;
            for (int halvings = 0; halvings <= HalvingsInTuning;)
            {
                bool better = false;
                for (int band = from; band < Bands.Count; band++)
                {
                    better |= TryStep(band, b => b with { Hertz = b.Hertz * Math.Pow(2, octaves) }, b => b with { Hertz = b.Hertz / Math.Pow(2, octaves) });
                    better |= TryStep(band, b => b with { Gain = b.Gain + db }, b => b with { Gain = b.Gain - db });
                    better |= TryStep(band, b => b with { Q = b.Q * Math.Pow(2, widths) }, b => b with { Q = b.Q / Math.Pow(2, widths) });
                }

                if (better) continue;
                (octaves, db, widths) = (octaves / 2, db / 2, widths / 2);
                halvings++;
            }
        }

        /// <summary>Moves a band one step either way when that lowers the error, and says whether it did.</summary>
        private bool TryStep(int band, Func<ParametricBand, ParametricBand> up, Func<ParametricBand, ParametricBand> down)
        {
            double now = Rms();
            foreach (var step in new[] { up, down })
            {
                var moved = Held(step(Bands[band]));
                if (moved == Bands[band]) continue;
                double[] response = Response(moved);
                var total = (double[])_total.Clone();
                for (int i = 0; i < Grid.Length; i++) total[i] += response[i] - _responses[band][i];
                if (Rms(total) >= now) continue;

                Bands[band] = moved;
                _responses[band] = response;
                Array.Copy(total, _total, total.Length);
                return true;
            }

            return false;
        }

        private double[] Response(ParametricBand band) => [.. Grid.Select(hertz => BandResponse.Db(band, hertz))];

        /// <summary>The root mean square error over the grid, with any rise past the most boost counted a hundred times over.</summary>
        private double Rms(double[] total)
        {
            double sum = 0;
            for (int i = 0; i < Grid.Length; i++)
            {
                double over = Math.Max(0, total[i] - MostBoost);
                sum += ((_target[i] - total[i]) * (_target[i] - total[i])) + (OverBoostWeight * over * over);
            }

            return Math.Sqrt(sum / Grid.Length);
        }
    }
}
