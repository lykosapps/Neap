namespace Neap.Core.Presets;

/// <summary>
/// The equaliser plot's maths: where a band sits, how a drag moves it,
/// and the tangents of the smooth line through the bands.
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

    /// <summary>How far, in pixels, the pointer has to move before a press on a point becomes a drag.</summary>
    public const double DragThreshold = 4;

    /// <summary>
    /// Where a dragged band goes: moved by how far the pointer has gone since
    /// the press, from where it was, in the headset's steps.
    /// </summary>
    /// <remarks>
    /// Moved by the distance, not put under the pointer: a press anywhere in
    /// a band's column grabs it, so putting it under the pointer would throw
    /// it off its value at the first twitch. A movement too small to mean a
    /// drag leaves it alone.
    /// </remarks>
    /// <param name="start">The band's value when pressed, in tenths.</param>
    /// <param name="down">Pixels moved down since the press.</param>
    /// <param name="minimum">The plot's lowest value, in tenths.</param>
    /// <param name="maximum">The plot's highest value, in tenths.</param>
    /// <param name="height">The plot's height, in pixels.</param>
    public static int Dragged(int start, double down, int minimum, int maximum, double height)
    {
        if (Math.Abs(down) < DragThreshold || height <= 0) return start;
        int moved = PresetStore.Snap((int)Math.Round(start - down * (maximum - minimum) / height));
        return Math.Clamp(moved, minimum, maximum);
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
