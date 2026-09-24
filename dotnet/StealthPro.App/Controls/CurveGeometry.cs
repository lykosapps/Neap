using Microsoft.UI.Xaml.Media;
using StealthPro.Core.Presets;
using Windows.Foundation;

namespace StealthPro.App.Controls;

/// <summary>
/// Draws an equaliser curve: a smooth line through the bands that never
/// overshoots between them.
/// </summary>
internal static class CurveGeometry
{
    /// <summary>A smooth line through the points; see <see cref="ResponseCurve.Slopes"/>.</summary>
    /// <param name="points">The bands, left to right.</param>
    /// <param name="closeAt">A height to close the shape down to for a filled area, or null for a line.</param>
    public static PathGeometry Through(IReadOnlyList<Point> points, double? closeAt = null)
    {
        var geometry = new PathGeometry();
        if (points.Count == 0) return geometry;

        var figure = new PathFigure { StartPoint = points[0], IsClosed = closeAt is not null, IsFilled = closeAt is not null };
        var slopes = ResponseCurve.Slopes(points.Select(p => (p.X, p.Y)).ToList());
        for (int i = 0; i < points.Count - 1; i++)
        {
            double run = (points[i + 1].X - points[i].X) / 3;
            figure.Segments.Add(new BezierSegment
            {
                Point1 = new Point(points[i].X + run, points[i].Y + slopes[i] * run),
                Point2 = new Point(points[i + 1].X - run, points[i + 1].Y - slopes[i + 1] * run),
                Point3 = points[i + 1],
            });
        }

        if (closeAt is double zero)
        {
            figure.Segments.Add(new LineSegment { Point = new Point(points[^1].X, zero) });
            figure.Segments.Add(new LineSegment { Point = new Point(points[0].X, zero) });
        }

        geometry.Figures.Add(figure);
        return geometry;
    }
}
