using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a quadratic segment of a general path.
/// </summary>
public class QuadPathSegment : PathSegment
{
    private readonly QuadCurve _curve;

    public QuadPathSegment(TwoDPoint start, TwoDPoint control, TwoDPoint end)
        : base(start, control, end)
    {
        _curve = new QuadCurve(start, control, end);
    }

    /// <summary>
    /// This method is used to determine the intersection points in X of this segment along
    /// the horizontal line at the given Y.
    /// </summary>
    /// <param name="point">A point on the line to find the intersections on.</param>
    /// <returns>An array of intersection points, if any.</returns>
    internal override double[] XIntersectionsWith(TwoDPoint point)
    {
        double a = Points[0].Y - 2 * Points[1].Y + Points[2].Y;
        double b = -2 * (Points[0].Y - Points[1].Y);
        double c = Points[0].Y - point.Y;

        return QuadCurve.Evaluate(a, b, c)
            .Select(t => _curve.GetPoint(t).X)
            .ToArray();
    }
}
