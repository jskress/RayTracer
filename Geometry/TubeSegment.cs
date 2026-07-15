using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a single, linearly-interpolated segment of a tube: the solid
/// formed by the union of every sphere obtained by interpolating center and radius between
/// two end spheres.  Its boundary is a tapered lateral (envelope) surface -- the points
/// where a ray is exactly tangent to one of the interpolated spheres -- plus whatever
/// portion of each end sphere isn't swallowed by a neighboring interpolated sphere further
/// along the segment.
/// </summary>
public class TubeSegment : Surface
{
    /// <summary>
    /// This property holds the center of the segment's starting sphere.
    /// </summary>
    public Point Start { get; set; }

    /// <summary>
    /// This property holds the radius of the segment's starting sphere.
    /// </summary>
    public double StartRadius { get; set; }

    /// <summary>
    /// This property holds the center of the segment's ending sphere.
    /// </summary>
    public Point End { get; set; }

    /// <summary>
    /// This property holds the radius of the segment's ending sphere.
    /// </summary>
    public double EndRadius { get; set; }

    private Vector _deltaCenter;
    private double _deltaRadius;
    private double _k2;

    /// <summary>
    /// This method precomputes the values that stay constant for the segment, regardless of
    /// the ray being tested.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _deltaCenter = End - Start;
        _deltaRadius = EndRadius - StartRadius;
        _k2 = _deltaCenter.Dot(_deltaCenter) - _deltaRadius * _deltaRadius;
    }

    /// <summary>
    /// This method returns a default bounding box that encloses both end spheres.
    /// </summary>
    /// <returns>A bounding box enclosing the segment.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        BoundingBox box = new BoundingBox();

        foreach (double dx in new[] { -StartRadius, StartRadius })
        foreach (double dy in new[] { -StartRadius, StartRadius })
        foreach (double dz in new[] { -StartRadius, StartRadius })
            box.Add(Start + new Vector(dx, dy, dz));

        foreach (double dx in new[] { -EndRadius, EndRadius })
        foreach (double dy in new[] { -EndRadius, EndRadius })
        foreach (double dz in new[] { -EndRadius, EndRadius })
            box.Add(End + new Vector(dx, dy, dz));

        return box;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the segment and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        AddLateralIntersections(ray, intersections);
        AddCapIntersections(ray, Start, StartRadius, _deltaCenter, _deltaRadius, intersections);
        AddCapIntersections(ray, End, EndRadius, -_deltaCenter, -_deltaRadius, intersections);
    }

    /// <summary>
    /// This method solves for, and adds, any intersections with the segment's tapered
    /// lateral (envelope) surface.  Substituting the ray equation into the sphere-family
    /// equation and applying the tangency condition (the point lies on exactly one
    /// interpolated sphere, and is the closest such sphere to the ray) eliminates the
    /// interpolation parameter algebraically, leaving a plain quadratic in the ray's own
    /// distance parameter.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void AddLateralIntersections(Ray ray, List<Intersection> intersections)
    {
        // K2 near zero means the radius changes at exactly the rate needed to make the
        // envelope's half-angle 90 degrees -- a flat-disk degenerate case we don't attempt
        // to model; only the end caps contribute in that case.
        if (_k2.Near(0))
            return;

        Vector q = ray.Origin - Start;
        double k0A = q.Dot(q) - StartRadius * StartRadius;
        double k0B = 2 * q.Dot(ray.Direction);
        double k0C = ray.Direction.Dot(ray.Direction);
        double k1A = -2 * (q.Dot(_deltaCenter) + StartRadius * _deltaRadius);
        double k1B = -2 * ray.Direction.Dot(_deltaCenter);

        double a = 4 * _k2 * k0C - k1B * k1B;
        double b = 4 * _k2 * k0B - 2 * k1A * k1B;
        double c = 4 * _k2 * k0A - k1A * k1A;

        foreach (double t in TubeCurveMath.SolveQuadratic(a, b, c))
        {
            double u = -(k1A + k1B * t) / (2 * _k2);

            if (u > 0 && u < 1)
            {
                Point point = ray.At(t);
                Point center = Start + _deltaCenter * u;

                intersections.Add(new PrecomputedNormalIntersection(this, t, point - center));
            }
        }
    }

    /// <summary>
    /// This method solves for, and adds, any intersections with one of the segment's end
    /// spheres, keeping only the points that lie on the outer boundary of the union of
    /// interpolated spheres (i.e., points that aren't swallowed by a neighboring sphere
    /// further along the segment).
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="center">The center of the end sphere to test.</param>
    /// <param name="radius">The radius of the end sphere to test.</param>
    /// <param name="deltaCenter">The vector from this end's center toward the other end's
    /// center.</param>
    /// <param name="deltaRadius">The difference between the other end's radius and this
    /// end's.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void AddCapIntersections(
        Ray ray, Point center, double radius, Vector deltaCenter, double deltaRadius,
        List<Intersection> intersections)
    {
        Vector sphereToRay = ray.Origin - center;
        double a = ray.Direction.Dot(ray.Direction);
        double b = 2 * ray.Direction.Dot(sphereToRay);
        double c = sphereToRay.Dot(sphereToRay) - radius * radius;

        foreach (double t in TubeCurveMath.SolveQuadratic(a, b, c))
        {
            Point point = ray.At(t);

            // Whether this point on the end sphere is on the union's outer boundary reduces
            // to the sign of the family equation's slope at this end (u = 0): the point is
            // swallowed by a neighboring sphere as soon as the family's radius grows faster,
            // there, than the point pulls away from the center.
            double k1 = -2 * ((point - center).Dot(deltaCenter) + radius * deltaRadius);
            bool isOnBoundary = _k2 < 0 ? k1 >= -_k2 : k1 >= 0;

            if (isOnBoundary)
                intersections.Add(new PrecomputedNormalIntersection(this, t, point - center));
        }
    }

    /// <summary>
    /// This method returns the normal for the segment.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will also be
    /// in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        return ((PrecomputedNormalIntersection) intersection).PrecomputedNormal;
    }
}
