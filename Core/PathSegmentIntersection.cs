using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents an intersection with a path segment
/// </summary>
public class PathSegmentIntersection : Intersection
{
    /// <summary>
    /// This holds the segment that was intersected.
    /// </summary>
    public PathSegment Segment { get; }

    public PathSegmentIntersection(Surface surface, double distance, PathSegment segment)
        : base(surface, distance)
    {
        Segment = segment;
    }
}
