using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class represents an intersection with a smooth triangle.
/// </summary>
public class SmoothTriangleIntersection : Intersection
{
    /// <summary>
    /// This holds the U value for the uv of the intersection point.
    /// </summary>
    public double U { get; }

    /// <summary>
    /// This holds the V value for the uv of the intersection point.
    /// </summary>
    public double V { get; }

    public SmoothTriangleIntersection(Surface surface, double distance, double u, double v)
        : base(surface, distance)
    {
        U = u;
        V = v;
    }
}
