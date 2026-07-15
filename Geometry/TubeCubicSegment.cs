using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a cubic-Bezier-curved segment of a tube: the solid formed by the
/// union of every sphere obtained by interpolating center and radius, as cubic Bezier
/// curves, across four control spheres (start, two control points, end).  This is the same
/// technique <see cref="TubeQuadSegment"/> uses (see <see cref="TubeCurveMath"/>), just one
/// curve degree higher -- which is exactly what POV-Ray's own sphere_sweep runs into for
/// its Catmull-Rom/B-spline segments: the family equation is now degree 6 in the curve
/// parameter u (rather than quadratic's degree 4), its resultant with the envelope
/// condition comes out to a fixed degree 10 in the ray's distance parameter t (rather than
/// degree 6), and everything else -- normalized sampling, trimming to the true degree,
/// residual-checked root recovery -- carries over unchanged, just at a larger, but still
/// entirely manageable, scale.
/// </summary>
public class TubeCubicSegment : Surface
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
    /// This property holds the center of the segment's first control sphere.
    /// </summary>
    public Point Control1 { get; set; }

    /// <summary>
    /// This property holds the radius of the segment's first control sphere.
    /// </summary>
    public double Control1Radius { get; set; }

    /// <summary>
    /// This property holds the center of the segment's second control sphere.
    /// </summary>
    public Point Control2 { get; set; }

    /// <summary>
    /// This property holds the radius of the segment's second control sphere.
    /// </summary>
    public double Control2Radius { get; set; }

    /// <summary>
    /// This property holds the center of the segment's ending sphere.
    /// </summary>
    public Point End { get; set; }

    /// <summary>
    /// This property holds the radius of the segment's ending sphere.
    /// </summary>
    public double EndRadius { get; set; }

    // Center(u) = Start + K1*u + K2*u^2 + K3*u^3, Radius(u) = StartRadius + K1r*u + K2r*u^2
    // + K3r*u^3 -- the power-basis form of a cubic Bezier.
    private Vector _k1;
    private Vector _k2;
    private Vector _k3;
    private double _k1R;
    private double _k2R;
    private double _k3R;
    private double _dotK1K1;
    private double _dotK1K2;
    private double _dotK1K3;
    private double _dotK2K2;
    private double _dotK2K3;
    private double _dotK3K3;

    /// <summary>
    /// This method precomputes the values that stay constant for the segment, regardless of
    /// the ray being tested.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _k1 = 3 * (Control1 - Start);
        _k2 = 3 * ((Start - Control1) + (Control2 - Control1));
        _k3 = (End - Start) - 3 * (Control2 - Control1);

        _k1R = 3 * (Control1Radius - StartRadius);
        _k2R = 3 * (StartRadius - 2 * Control1Radius + Control2Radius);
        _k3R = (EndRadius - StartRadius) - 3 * (Control2Radius - Control1Radius);

        _dotK1K1 = _k1.Dot(_k1);
        _dotK1K2 = _k1.Dot(_k2);
        _dotK1K3 = _k1.Dot(_k3);
        _dotK2K2 = _k2.Dot(_k2);
        _dotK2K3 = _k2.Dot(_k3);
        _dotK3K3 = _k3.Dot(_k3);
    }

    /// <summary>
    /// This method returns a default bounding box that encloses all four control spheres,
    /// each conservatively expanded to the segment's largest radius.  This is a valid (if
    /// not tightest-possible) bound: a cubic Bezier curve, and a cubic Bezier radius, both
    /// stay within the convex hull of their control values.
    /// </summary>
    /// <returns>A bounding box enclosing the segment.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        double radius = Math.Max(
            Math.Max(StartRadius, Control1Radius), Math.Max(Control2Radius, EndRadius));
        BoundingBox box = new BoundingBox();

        foreach (Point point in new[] { Start, Control1, Control2, End })
        foreach (double dx in new[] { -radius, radius })
        foreach (double dy in new[] { -radius, radius })
        foreach (double dz in new[] { -radius, radius })
            box.Add(point + new Vector(dx, dy, dz));

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
        AddCapIntersections(ray, Start, StartRadius, intersections);
        AddCapIntersections(ray, End, EndRadius, intersections);
    }

    /// <summary>
    /// This method solves for, and adds, any intersections with the segment's curved
    /// lateral (envelope) surface.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void AddLateralIntersections(Ray ray, List<Intersection> intersections)
    {
        (double tMin, double tMax) = BoundingBoxHitInterval(ray);

        if (!(tMax > tMin))
            return;

        Vector q = ray.Origin - Start;
        Vector d = ray.Direction;
        double dotQQ = q.Dot(q);
        double dotQD = q.Dot(d);
        double dotDD = d.Dot(d);
        double dotQK1 = q.Dot(_k1);
        double dotDK1 = d.Dot(_k1);
        double dotQK2 = q.Dot(_k2);
        double dotDK2 = d.Dot(_k2);
        double dotQK3 = q.Dot(_k3);
        double dotDK3 = d.Dot(_k3);

        // g(u, t) coefficients: g[k] is the coefficient of u^k, itself a polynomial in t
        // (indices 0, 1, 2 are the t^0, t^1, t^2 coefficients).  Its resultant with dg/du
        // is a fixed degree-10 polynomial in t, so 11 sample points pin it down exactly.
        double[][] g =
        [
            [dotQQ - StartRadius * StartRadius, 2 * dotQD, dotDD],
            [-2 * _k1R * StartRadius - 2 * dotQK1, -2 * dotDK1, 0],
            [-_k1R * _k1R - 2 * _k2R * StartRadius + _dotK1K1 - 2 * dotQK2, -2 * dotDK2, 0],
            [-2 * _k1R * _k2R - 2 * _k3R * StartRadius + 2 * _dotK1K2 - 2 * dotQK3, -2 * dotDK3, 0],
            [-2 * _k1R * _k3R - _k2R * _k2R + 2 * _dotK1K3 + _dotK2K2, 0, 0],
            [-2 * _k2R * _k3R + 2 * _dotK2K3, 0, 0],
            [-_k3R * _k3R + _dotK3K3, 0, 0]
        ];

        foreach ((double t, double u) in TubeCurveMath.FindEnvelopeHits(g, 11, tMin, tMax))
        {
            Point point = ray.At(t);
            Point center = Start + u * _k1 + u * u * _k2 + u * u * u * _k3;

            intersections.Add(new PrecomputedNormalIntersection(this, t, point - center));
        }
    }

    /// <summary>
    /// This method solves for, and adds, any intersections with one of the segment's end
    /// spheres, keeping only the points that lie on the outer boundary of the union of
    /// interpolated spheres.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="center">The center of the end sphere to test.</param>
    /// <param name="radius">The radius of the end sphere to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void AddCapIntersections(
        Ray ray, Point center, double radius, List<Intersection> intersections)
    {
        Vector sphereToRay = ray.Origin - center;
        double a = ray.Direction.Dot(ray.Direction);
        double b = 2 * ray.Direction.Dot(sphereToRay);
        double c = sphereToRay.Dot(sphereToRay) - radius * radius;

        foreach (double t in TubeCurveMath.SolveQuadratic(a, b, c))
        {
            Point point = ray.At(t);
            Vector qp = point - Start;
            double dotQpQp = qp.Dot(qp);
            double dotQpK1 = qp.Dot(_k1);
            double dotQpK2 = qp.Dot(_k2);
            double dotQpK3 = qp.Dot(_k3);

            double[] f =
            [
                dotQpQp - StartRadius * StartRadius,
                -2 * _k1R * StartRadius - 2 * dotQpK1,
                -_k1R * _k1R - 2 * _k2R * StartRadius + _dotK1K1 - 2 * dotQpK2,
                -2 * _k1R * _k2R - 2 * _k3R * StartRadius + 2 * _dotK1K2 - 2 * dotQpK3,
                -2 * _k1R * _k3R - _k2R * _k2R + 2 * _dotK1K3 + _dotK2K2,
                -2 * _k2R * _k3R + 2 * _dotK2K3,
                -_k3R * _k3R + _dotK3K3
            ];

            if (TubeCurveMath.IsOnOuterBoundary(f))
                intersections.Add(new PrecomputedNormalIntersection(this, t, point - center));
        }
    }

    /// <summary>
    /// This method computes the interval, along the given ray, over which our bounding box
    /// is intersected -- used to choose numerically well-scaled sample points for
    /// reconstructing the resultant polynomial.
    /// </summary>
    private (double Min, double Max) BoundingBoxHitInterval(Ray ray)
    {
        BoundingBox box = BoundingBox ?? GetDefaultBoundingBox();

        return box.GetIntersections(ray);
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
