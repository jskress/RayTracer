using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents the surface for a linear path segment.
/// </summary>
public class LinearPathSurface : PathSurface
{
    private readonly Parallelogram _parallelogram;

    public LinearPathSurface(LinearPathSegment segment, double minimumY, double maximumY)
        : base(minimumY, maximumY)
    {
        Point point0 = new Point(segment.Points[0].X, minimumY, segment.Points[0].Y);
        Point point1 = new Point(segment.Points[1].X, minimumY, segment.Points[1].Y);
        Point point2 = new Point(segment.Points[0].X, maximumY, segment.Points[0].Y);

        _parallelogram = new Parallelogram
        {
            Point = point0,
            Side1 = point1 - point0,
            Side2 = point2 - point0
        };
    }

    /// <summary>
    /// This method is used to locate the intersection point, if any, where the given ray
    /// intersects this path surface.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>An array of tuples containing the intersection distance and normal vector
    /// pairs.
    /// If the ray doesn't intersect the surface, the array will be <c>null</c>.</returns>
    public override SimpleIntersection[] GetIntersection(Ray ray)
    {
        double intersection = _parallelogram.GetIntersection(ray);
        
        return double.IsNaN(intersection)
            ? null
            : [new SimpleIntersection(intersection, _parallelogram.SurfaceNormaAt(null, null))];
    }
}
