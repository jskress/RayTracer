using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents the surface for a cubic Bézier curve path segment.
/// </summary>
public class CubicPathSurface : PathSurface
{
    private readonly CubicCurve _curve;

    public CubicPathSurface(CubicPathSegment segment, double minimumY, double maximumY)
        : base(minimumY, maximumY)
    {
        _curve = new CubicCurve(
            segment.Points[0], segment.Points[1], segment.Points[2],
            segment.Points[3]);
    }

    /// <summary>
    /// This method is used to locate the intersection point, if any, where the given ray
    /// intersects this path surface.
    /// </summary>
    /// <remarks>
    /// Algorithm from: https://www.tumblr.com/floorplanner-techblog/66681002205/computing-the-intersection-between-linear-and
    /// </remarks>
    /// <param name="ray">The ray to test.</param>
    /// <returns>An array of tuples containing the intersection distance and normal vector
    /// pairs.
    /// If the ray doesn't intersect the surface, the array will be <c>null</c>.</returns>
    public override SimpleIntersection[] GetIntersection(Ray ray)
    {
        Point point = ray.Origin + ray.Direction;
        TwoDPoint lineA = new TwoDPoint(ray.Origin.X, ray.Origin.Z);
        TwoDPoint lineB = new TwoDPoint(point.X, point.Z);

        return _curve.GetIntersectionData(lineA, lineB)
            .Select(data => IntersectionDataAt(ray, data.Item1, data.Item2))
            .ToArray();
    }

    /// <summary>
    /// This method is used to convert the given 2D intersection distance along the curve
    /// and intersection point into the appropriate ray distance and normal vector.
    /// </summary>
    /// <param name="ray">The ray we are working with.</param>
    /// <param name="t">The intersection distance along our curve.</param>
    /// <param name="intersectionPoint">The point of intersection in the X/Z plane.</param>
    /// <returns>The intersection information or <c>null</c>, if the translation of the
    /// intersection point to 3D ends up beyond our min/max Y bounds.</returns>
    private SimpleIntersection IntersectionDataAt(Ray ray, double t, TwoDPoint intersectionPoint)
    {
        double t3d = GetRayDistance(ray, intersectionPoint);

        return double.IsNaN(t3d) ? null : new SimpleIntersection(t3d, _curve.NormalAt(t));
    }
}
