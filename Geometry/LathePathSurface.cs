using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for a surface that is used to render a general path segment for
/// a lathe.
/// </summary>
public class LathePathSurface
{
    private readonly IPathSegment _segment;

    public LathePathSurface(IPathSegment segment)
    {
        _segment = segment;
    }

    /// <summary>
    /// This method is used to determine any intersections between the 2D rays and this
    /// path segment.
    /// </summary>
    /// <param name="surface">The surface we are supporting.</param>
    /// <param name="ray">The 3D ray we are evaluating.</param>
    /// <param name="theta">The rotational angle of the ray.</param>
    /// <param name="shapeRay">The ray projected to the X/Y plane, where the 2D shape is.</param>
    /// <returns>An enumeration of the intersections found with this segment.</returns>
    internal IEnumerable<Intersection> GetIntersections(
        Surface surface, Ray ray, double theta, TwoDRay shapeRay)
    {
        return _segment.GetIntersections(shapeRay)
            .Select(intersection => Get3DIntersections(surface, ray, theta, intersection))
            .SelectMany(intersections => intersections);
    }

    /// <summary>
    /// This method is used to resolve an intersection with the 2D shape and the circle
    /// it falls on.
    /// </summary>
    /// <param name="surface">The surface we are supporting.</param>
    /// <param name="ray">The 3D ray we are evaluating.</param>
    /// <param name="theta">The rotational angle of the ray.</param>
    /// <param name="withShape">The intersection point with the shape.</param>
    /// <returns>The 3D intersection points, if any exist.</returns>
    private static IEnumerable<Intersection> Get3DIntersections(
        Surface surface, Ray ray, double theta, TwoDIntersection withShape)
    {
        double t = (withShape.Point.Y - ray.Origin.Y) / ray.Direction.Y;

        if (t < 0)
            return [null];

        Point rotatedPoint = ray.At(t);
        double rho = Math.Sqrt(rotatedPoint.X * rotatedPoint.X + rotatedPoint.Z * rotatedPoint.Z);

        // if (Math.Abs(rho - withShape.Point.X) > DoubleExtensions.Epsilon)
        //     return [null]; // numerical filter

        Point point = Transforms.RotateAroundY(-theta, true) * rotatedPoint;
        Vector radial = new Vector(rotatedPoint.X, 0, rotatedPoint.Z);
        TwoDVector n = withShape.TwoDNormal;
        Vector nRotated = (radial * n.X + Directions.Up * n.Y).Unit;
        Vector normal = Transforms.RotateAroundY(-theta, true) *
                        nRotated;

        return [new PrecomputedNormalIntersection(surface, t, normal)];
    }

    /// <summary>
    /// This method is used to find the points of intersection along the given ray of with
    /// a circle of the specified radius.
    /// Zero, one or two values will be returned.
    /// No value returned will be less than zero.
    /// </summary>
    /// <param name="ray">The ray to work with.</param>
    /// <param name="radius">The radius of the circle</param>
    /// <returns>The values of t that represent the points of intersection.</returns>
    public static IEnumerable<double> GetTOnCircle(TwoDRay ray, double radius)
    {
        TwoDVector sphereToRay = ray.Origin - TwoDPoint.Zero;
        double a = ray.Direction.Dot(ray.Direction);
        double b = 2 * ray.Direction.Dot(sphereToRay);
        double c = sphereToRay.Dot(sphereToRay) - radius * radius;
        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return [];

        discriminant = Math.Sqrt(discriminant);
        a *= 2;
        b = -b;

        double t1 = (b - discriminant) / a;
        double t2 = (b + discriminant) / a;

        return t1 < 0 && t2 < 0
            ? []
            : t1 >= 0 && t2 >= 0
                ? [t1, t2]
                : t1 >= 0 ? [t1] : [t2];
    }

    /// <summary>
    /// This method takes the 2D intersection point with a circle and promotes it to a 3D
    /// intersection, if possible.
    /// Otherwise, <c>null</c> will be returned.
    /// </summary>
    /// <param name="surface">The surface we are supporting.</param>
    /// <param name="ray">The 3D ray we are evaluating.</param>
    /// <param name="withShape">The intersection point with the shape.</param>
    /// <param name="circleRay">The projected ray for testing the circle.</param>
    /// <param name="t">The intersection distance along the circle ray.</param>
    /// <returns>A 3D intersection, or <c>null</c>.</returns>
    private static PrecomputedNormalIntersection PromoteTo3DIntersection(
        Surface surface, Ray ray, TwoDIntersection withShape, TwoDRay circleRay, double t)
    {
        TwoDPoint p = circleRay.At(t);
        Point point = new Point(p.X, withShape.Point.Y, p.Y);

        if (ray.Contains(point))
        {
            TwoDVector n = p - TwoDPoint.Zero;
            Vector normal = new Vector(n.X, withShape.TwoDNormal.Y, n.Y);

            t = (point - ray.Origin).Magnitude;

            return new PrecomputedNormalIntersection(surface, t, normal);
        }

        return null;
    }
}
