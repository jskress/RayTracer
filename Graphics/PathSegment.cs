using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a segment of a general path.
/// </summary>
public abstract class PathSegment
{
    /// <summary>
    /// This property holds the set of points, if any, the segment needs.
    /// </summary>
    internal TwoDPoint[] Points { get; }

    protected PathSegment(params TwoDPoint[] points)
    {
        Points = points;
    }

    /// <summary>
    /// This method is used to determine the intersection points in X of this segment along
    /// the horizontal line at the given Y.
    /// </summary>
    /// <param name="y">The Y coordinate of the line to find the intersections on.</param>
    /// <returns>An array of intersection points, if any.</returns>
    internal abstract double[] XIntersectionsWith(double y);
}

/// <summary>
/// This class represents a linear segment of a general path.
/// </summary>
public class LinearPathSegment : PathSegment
{
    private readonly double _minX;
    private readonly double _maxX;

    internal LinearPathSegment(TwoDPoint start, TwoDPoint end)
        : base(start, end)
    {
        _minX = Math.Min(start.X, end.X);
        _maxX = Math.Max(start.X, end.X);
    }

    /// <summary>
    /// This method is used to determine the intersection points in X of this segment along
    /// the horizontal line at the given Y.
    /// </summary>
    /// <param name="y">The Y coordinate of the line to find the intersections on.</param>
    /// <returns>An array of intersection points, if any.</returns>
    internal override double[] XIntersectionsWith(double y)
    {
        double x = (y - Points[0].Y) * (Points[1].X - Points[0].X) /
            (Points[1].Y - Points[0].Y) + Points[0].X;

        return x >= _minX && x <= _maxX ? [x] : [];
    }
}

/// <summary>
/// This class represents a quadratic segment of a general path.
/// </summary>
internal class QuadPathSegment : PathSegment
{
    internal QuadPathSegment(TwoDPoint start, TwoDPoint control, TwoDPoint end)
        : base(start, control, end) {}

    /// <summary>
    /// This method is used to determine the intersection points in X of this segment along
    /// the horizontal line at the given Y.
    /// </summary>
    /// <param name="y">The Y coordinate of the line to find the intersections on.</param>
    /// <returns>An array of intersection points, if any.</returns>
    internal override double[] XIntersectionsWith(double y)
    {
        return [];
    }
}

/// <summary>
/// This class represents a cubic segment of a general path.
/// </summary>
internal class CubicPathSegment : PathSegment
{
    internal CubicPathSegment(TwoDPoint start, TwoDPoint control1, TwoDPoint control2, TwoDPoint end)
        : base(start, control1, control2, end) {}

    /// <summary>
    /// This method is used to determine the intersection points in X of this segment along
    /// the horizontal line at the given Y.
    /// </summary>
    /// <param name="y">The Y coordinate of the line to find the intersections on.</param>
    /// <returns>An array of intersection points, if any.</returns>
    internal override double[] XIntersectionsWith(double y)
    {
        return [];
    }
}
