using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a linear segment of a general path.
/// </summary>
public class LinearPathSegment : PathSegment
{
    private readonly double _minX;
    private readonly double _minY;
    private readonly double _maxX;
    private readonly double _maxY;

    internal LinearPathSegment(TwoDPoint start, TwoDPoint end)
        : base(start, end)
    {
        _minX = Math.Min(start.X, end.X);
        _minY = Math.Min(start.Y, end.Y);
        _maxX = Math.Max(start.X, end.X);
        _maxY = Math.Max(start.Y, end.Y);
    }

    /// <summary>
    /// This method is used to determine the intersection points in X of this segment along
    /// the horizontal line at the given Y.
    /// </summary>
    /// <param name="point">A point on the line to find the intersections on.</param>
    /// <returns>An array of intersection points, if any.</returns>
    internal override double[] XIntersectionsWith(TwoDPoint point)
    {
        double y = point.Y;

        if (y < _minY || y > _maxY)
            return Empty;

        // If the line is vertical, we've got an intersection (without divide by zero issues)
        if (_minX.Near(_maxX))
            return [_minX];

        double x = (y - Points[0].Y) * (Points[1].X - Points[0].X) /
            (Points[1].Y - Points[0].Y) + Points[0].X;

        return [x];
    }
}
