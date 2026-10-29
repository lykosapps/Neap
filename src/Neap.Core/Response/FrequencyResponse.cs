using System.Globalization;

namespace Neap.Core.Response;

/// <summary>Why a file could not be read as a frequency response.</summary>
public enum ResponseProblem
{
    None,

    /// <summary>No line held a frequency and a level.</summary>
    NoPoints,

    /// <summary>Too few points to draw a curve from.</summary>
    TooFewPoints,

    /// <summary>A frequency was not above zero, or the frequencies did not rise.</summary>
    NotRising,
}

/// <summary>What reading a frequency response came to: the curve, or why there is none.</summary>
/// <param name="Curve">The curve, or null when the text is not one.</param>
/// <param name="Problem">Why not, when it is not.</param>
public readonly record struct ResponseRead(FrequencyResponse? Curve, ResponseProblem Problem);

/// <summary>A level at each frequency: a headphone's measured response, or a target to aim for.</summary>
/// <remarks>
/// <para>
/// Read from the plain text that measurement sites and AutoEq share: a
/// frequency and a level on each line, split by a comma, a semicolon, a tab or
/// spaces, with any heading or comment lines left out. Only the first two
/// numbers on a line are used.
/// </para>
/// <para>
/// Levels between points are read on a logarithmic frequency scale, as the
/// curve is drawn, and held at the first and last points beyond the ends.
/// </para>
/// </remarks>
public sealed class FrequencyResponse
{
    /// <summary>The fewest points a curve can be drawn from.</summary>
    public const int FewestPoints = 10;

    /// <summary>The frequency curves are lined up at, as the measurement sites do.</summary>
    public const double AlignedAt = 1000;

    private readonly double[] _hertz;
    private readonly double[] _db;

    private FrequencyResponse(double[] hertz, double[] db)
    {
        _hertz = hertz;
        _db = db;
    }

    /// <summary>The points, by frequency.</summary>
    public IReadOnlyList<(double Hertz, double Db)> Points => [.. _hertz.Zip(_db)];

    /// <summary>Reads a frequency response from text.</summary>
    /// <param name="text">The file's text.</param>
    public static ResponseRead Read(string text)
    {
        var hertz = new List<double>();
        var db = new List<double>();
        foreach (string line in text.Split('\n'))
        {
            string[] parts = line.Split([',', ';', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 2
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double f)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double level))
            {
                continue;
            }

            if (f <= 0 || (hertz.Count > 0 && f <= hertz[^1])) return new ResponseRead(null, ResponseProblem.NotRising);
            hertz.Add(f);
            db.Add(level);
        }

        if (hertz.Count == 0) return new ResponseRead(null, ResponseProblem.NoPoints);
        if (hertz.Count < FewestPoints) return new ResponseRead(null, ResponseProblem.TooFewPoints);
        return new ResponseRead(new FrequencyResponse([.. hertz], [.. db]), ResponseProblem.None);
    }

    /// <summary>The level at a frequency.</summary>
    /// <param name="hertz">The frequency.</param>
    public double At(double hertz)
    {
        if (hertz <= _hertz[0]) return _db[0];
        if (hertz >= _hertz[^1]) return _db[^1];
        int above = Array.BinarySearch(_hertz, hertz);
        if (above >= 0) return _db[above];
        above = ~above;
        int below = above - 1;
        double t = Math.Log(hertz / _hertz[below]) / Math.Log(_hertz[above] / _hertz[below]);
        return _db[below] + (t * (_db[above] - _db[below]));
    }

    /// <summary>The same curve moved up or down so it is at 0 dB at <see cref="AlignedAt"/>.</summary>
    public FrequencyResponse Aligned()
    {
        double at = At(AlignedAt);
        return new FrequencyResponse(_hertz, [.. _db.Select(level => level - at)]);
    }

    /// <summary>The change that brings this curve to a target, at each of this curve's points, with both lined up at 1 kHz.</summary>
    /// <param name="target">The curve to reach.</param>
    public IReadOnlyList<(double Hertz, double Db)> Toward(FrequencyResponse target)
    {
        var from = Aligned();
        var to = target.Aligned();
        return [.. from._hertz.Select((f, i) => (f, to.At(f) - from._db[i]))];
    }

    /// <summary>The same curve with a change added at every point, such as the equaliser's.</summary>
    /// <param name="change">The decibels to add at a frequency.</param>
    public FrequencyResponse Plus(Func<double, double> change) =>
        new(_hertz, [.. _hertz.Select((f, i) => _db[i] + change(f))]);
}
