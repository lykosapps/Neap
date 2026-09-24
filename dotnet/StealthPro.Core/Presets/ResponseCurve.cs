namespace StealthPro.Core.Presets;

/// <summary>
/// The equaliser plot's maths: where a band sits, which value a height
/// means, and the tangents of the smooth line through the bands.
/// </summary>
/// <remarks>
/// <para>
/// Values are tenths of a decibel, as the headset stores them. Heights run
/// down the screen, so the top of the plot is the maximum and its bottom the
/// minimum.
/// </para>
/// <para>
/// The line never overshoots. The tangents are monotone cubic
/// (Fritsch–Carlson), so the curve cannot bulge past a neighbouring band's
/// value. An ordinary Catmull-Rom spline does, and on an equaliser that
/// draws gain the headset is not applying.
/// </para>
/// </remarks>
public static class ResponseCurve
{
    /// <summary>The horizontal centre of a band's column.</summary>
    public static double X(int band, int bands, double width) =>
        bands == 0 ? 0 : (band + 0.5) * width / bands;

    /// <summary>The height of a value on the plot.</summary>
    public static double Y(double tenths, int minimum, int maximum, double top, double bottom)
    {
        double span = maximum - minimum;
        double t = span <= 0 ? 0.5 : (maximum - tenths) / span;
        return top + t * (bottom - top);
    }

    /// <summary>The value at a height, rounded to a tenth and held inside the range.</summary>
    public static int TenthsAt(double y, int minimum, int maximum, double top, double bottom)
    {
        double height = bottom - top;
        double t = height <= 0 ? 0 : (y - top) / height;
        return (int)Math.Round(Math.Clamp(maximum - t * (maximum - minimum), minimum, maximum));
    }

    /// <summary>The tangent at each point, for a cubic through them that never overshoots.</summary>
    public static double[] Slopes(IReadOnlyList<(double X, double Y)> points)
    {
        int n = points.Count;
        var slope = new double[n];
        if (n < 2) return slope;

        var secant = new double[n - 1];
        for (int i = 0; i < n - 1; i++)
        {
            double run = points[i + 1].X - points[i].X;
            secant[i] = run == 0 ? 0 : (points[i + 1].Y - points[i].Y) / run;
        }

        slope[0] = secant[0];
        slope[n - 1] = secant[n - 2];
        for (int i = 1; i < n - 1; i++)
            slope[i] = secant[i - 1] * secant[i] <= 0 ? 0 : (secant[i - 1] + secant[i]) / 2;

        for (int i = 0; i < n - 1; i++)
        {
            if (secant[i] == 0) { slope[i] = 0; slope[i + 1] = 0; continue; }
            double a = slope[i] / secant[i], b = slope[i + 1] / secant[i];
            double size = a * a + b * b;
            if (size <= 9) continue;
            double scale = 3 / Math.Sqrt(size);
            slope[i] = scale * a * secant[i];
            slope[i + 1] = scale * b * secant[i];
        }
        return slope;
    }
}
