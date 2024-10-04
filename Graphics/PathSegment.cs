using RayTracer.Basics;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a segment of a general path.
/// </summary>
public abstract class PathSegment
{
    /// <summary>
    /// A handy constant for an empty intersection result.
    /// </summary>
    protected static readonly double[] Empty = [];

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
    /// <param name="point">A point on the line to find the intersections on.</param>
    /// <returns>An array of intersection points, if any.</returns>
    internal abstract double[] XIntersectionsWith(TwoDPoint point);
}
