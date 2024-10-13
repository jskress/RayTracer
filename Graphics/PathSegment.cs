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
    internal TwoDPoint[] Points { get; private set; }

    protected PathSegment(params TwoDPoint[] points)
    {
        Points = points;
    }

    /// <summary>
    /// This method is used to reverse the order of the points in this segment.
    /// </summary>
    internal void Reverse()
    {
        Points = Points.Reverse().ToArray();
    }
}
