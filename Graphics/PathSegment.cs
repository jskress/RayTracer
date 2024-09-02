using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Geometry;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a segment of a general path.
/// </summary>
public abstract class PathSegment
{
    /// <summary>
    /// This property holds the A coefficient.
    /// </summary>
    internal TwoDPoint A { get; }

    /// <summary>
    /// This property holds the B coefficient.
    /// </summary>
    internal TwoDPoint B { get; }

    /// <summary>
    /// This property holds the C coefficient.
    /// </summary>
    internal TwoDPoint C { get; }

    /// <summary>
    /// This property holds the D coefficient.
    /// </summary>
    internal TwoDPoint D { get; }

    protected PathSegment(TwoDPoint a, TwoDPoint b, TwoDPoint c, TwoDPoint d)
    {
        A = a;
        B = b;
        C = c;
        D = d;
    }

    /// <summary>
    /// This method must be provided by subclasses to determine whether the given
    /// ray intersects the geometry and, if so, where.
    /// </summary>
    /// <param name="surface">The surface we are acting on behalf of.</param>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public abstract void AddIntersections(
        ExtrudedSurface surface, Ray ray, List<Intersection> intersections);

    /// <summary>
    /// This method performs the final checks to see if a ray intersects this segment.
    /// </summary>
    /// <param name="surface">The surface we are acting on behalf of.</param>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    /// <param name="distances">The set of distances to check.</param>
    protected void FinalizeIntersections(
        ExtrudedSurface surface, Ray ray, List<Intersection> intersections, double[] distances)
    {
        intersections.AddRange(from distance in distances
            where distance is >= 0 and <= 1
            let k = ray.Direction.X.Near(0)
                ? (distance * (distance * (distance * A.Y + B.Y) + C.Y) + D.Y - ray.Origin.Z) / ray.Direction.Z
                : (distance * (distance * (distance * A.X + B.X) + C.X) + D.X - ray.Origin.X) / ray.Direction.X
            let height = ray.Origin.Y + k * ray.Direction.Y
            where height >= surface.MinimumY && height <= surface.MaximumY
            select new PathSegmentIntersection(surface, distance / ray.Direction.Magnitude, this));
    }
}

/// <summary>
/// This class represents a linear segment of a general path.
/// </summary>
public class LinearPathSegment : PathSegment
{
    public LinearPathSegment(TwoDPoint c, TwoDPoint d)
        : base(TwoDPoint.Zero, TwoDPoint.Zero, c, d) {}

    /// <summary>
    /// This method is used to determine whether the given ray intersects the path segment and,
    /// if so, where.
    /// </summary>
    /// <param name="surface">The surface we are acting on behalf of.</param>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(
        ExtrudedSurface surface, Ray ray, List<Intersection> intersections)
    {
        double x0 = C.X * ray.Direction.Z - C.Y * ray.Direction.X;
        double x1 = ray.Direction.Z * (D.X - ray.Origin.X) - ray.Direction.X * (D.Y - ray.Origin.Z);

        if (!x0.Near(0))
            FinalizeIntersections(surface, ray, intersections, [x1 / x0]);
    }
}

/// <summary>
/// This class represents a bezier segment of a general path.
/// </summary>
public class BezierPathSegment : PathSegment
{
    public BezierPathSegment(TwoDPoint a, TwoDPoint b, TwoDPoint c, TwoDPoint d)
        : base(a, b, c, d) {}

    /// <summary>
    /// This method is used to determine whether the given ray intersects the path segment and,
    /// if so, where.
    /// </summary>
    /// <param name="surface">The surface we are acting on behalf of.</param>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(
        ExtrudedSurface surface, Ray ray, List<Intersection> intersections)
    {
        double dx = ray.Direction.X;
        double dz = ray.Direction.Z;
        double[] coefficients = [
            A.X * dz - A.Y * dx,
            B.X * dz - B.Y * dx,
            C.X * dz - C.Y * dx,
            dz * (D.X - ray.Origin.X) - dx * (D.Y - ray.Origin.Z)
        ];
        double[] distances = Polynomials.Solve(coefficients);

        if (distances.Length > 0)
            FinalizeIntersections(surface, ray, intersections, distances);
    }
}
