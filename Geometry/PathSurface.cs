using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for a surface that is used to render a general path segment.
/// It is assumed that the path segment is in the X/Z plane.
/// </summary>
public abstract class PathSurface
{
    /// <summary>
    /// This holds the low end of the surface
    /// </summary>
    protected double MinimumY { get; }

    /// <summary>
    /// This holds the high end of the surface
    /// </summary>
    protected double MaximumY { get; }

    protected PathSurface(double minimumY, double maximumY)
    {
        MinimumY = minimumY;
        MaximumY = maximumY;
    }

    /// <summary>
    /// This method is used to locate the intersection point, if any, where the given ray
    /// intersects this path surface.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>A tuple containing the intersection distance and normal vector.  If the
    /// ray doesn't intersect the surface, the distance will be <c>NaN</c> and the normal
    /// will be <c>null</c>.</returns>
    public abstract (double, Vector) GetIntersection(Ray ray);
}

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
    /// <returns>A tuple containing the intersection distance and normal vector.  If the
    /// ray doesn't intersect the surface, the distance will be <c>NaN</c> and the normal
    /// will be <c>null</c>.</returns>
    public override (double, Vector) GetIntersection(Ray ray)
    {
        double intersection = _parallelogram.GetIntersection(ray);
        
        return double.IsNaN(intersection)
            ? (intersection, null)
            : (intersection, _parallelogram.SurfaceNormaAt(null, null));
    }
}
