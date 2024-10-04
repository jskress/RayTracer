using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a cubic segment of a general path.
/// </summary>
public class CubicPathSegment : PathSegment
{
    private readonly CubicCurve _curve;

    internal CubicPathSegment(TwoDPoint start, TwoDPoint control1, TwoDPoint control2, TwoDPoint end)
        : base(start, control1, control2, end)
    {
        _curve = new CubicCurve(start, control1, control2, end);
    }

    /// <summary>
    /// This method is used to determine the intersection points in X of this segment along
    /// the horizontal line at the given Y.
    /// </summary>
    /// <param name="point">A point on the line to find the intersections on.</param>
    /// <returns>An array of intersection points, if any.</returns>
    internal override double[] XIntersectionsWith(TwoDPoint point)
    {
        return _curve.GetXIntersectionsFor(point);
    }
}
