using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a quadratic-Bezier-curved segment of a tube: the solid formed by
/// the union of every sphere obtained by interpolating center and radius, as quadratic
/// Bezier curves, across three control spheres (start, control, end).  Unlike the linear
/// <see cref="TubeSegment"/>, eliminating the curve parameter between the envelope
/// condition and the sphere-family equation doesn't reduce to a simple explicit formula --
/// see <see cref="TubeCurveMath"/> for how it's solved instead.
/// </summary>
public class TubeQuadSegment : Surface
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
    /// This property holds the center of the segment's control sphere.
    /// </summary>
    public Point Control { get; set; }

    /// <summary>
    /// This property holds the radius of the segment's control sphere.
    /// </summary>
    public double ControlRadius { get; set; }

    /// <summary>
    /// This property holds the center of the segment's ending sphere.
    /// </summary>
    public Point End { get; set; }

    /// <summary>
    /// This property holds the radius of the segment's ending sphere.
    /// </summary>
    public double EndRadius { get; set; }

    // Center(u) = Start - 2u*B + u^2*A, Radius(u) = StartRadius - 2u*Rb + u^2*Ra -- the
    // power-basis form of a quadratic Bezier (A is its "acceleration" term, B its initial
    // "velocity" term), which keeps the resulting algebra close to the linear segment's.
    private Vector _a;
    private Vector _b;
    private double _ra;
    private double _rb;
    private double _dotAA;
    private double _dotAB;
    private double _dotBB;

    /// <summary>
    /// This method precomputes the values that stay constant for the segment, regardless of
    /// the ray being tested.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _b = Start - Control;
        _a = _b - (Control - End);
        _rb = StartRadius - ControlRadius;
        _ra = _rb - (ControlRadius - EndRadius);
        _dotAA = _a.Dot(_a);
        _dotAB = _a.Dot(_b);
        _dotBB = _b.Dot(_b);
    }

    /// <summary>
    /// This method returns a default bounding box that encloses all three control spheres,
    /// each conservatively expanded to the segment's largest radius.  This is a valid (if
    /// not tightest-possible) bound: a quadratic Bezier curve, and a quadratic Bezier
    /// radius, both stay within the convex hull of their control values.
    /// </summary>
    /// <returns>A bounding box enclosing the segment.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        double radius = Math.Max(StartRadius, Math.Max(ControlRadius, EndRadius));
        BoundingBox box = new BoundingBox();

        foreach (Point point in new[] { Start, Control, End })
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
        double dotQB = q.Dot(_b);
        double dotBD = _b.Dot(d);
        double dotQA = q.Dot(_a);
        double dotAD = _a.Dot(d);

        // g(u, t) coefficients: g[k] is the coefficient of u^k, itself a polynomial in t
        // (indices 0, 1, 2 are the t^0, t^1, t^2 coefficients).  Its resultant with dg/du
        // is a fixed degree-6 polynomial in t, so 7 sample points pin it down exactly.
        double[][] g =
        [
            [dotQQ - StartRadius * StartRadius, 2 * dotQD, dotDD],
            [4 * (StartRadius * _rb + dotQB), 4 * dotBD, 0],
            [-2 * StartRadius * _ra - 4 * _rb * _rb + 4 * _dotBB - 2 * dotQA, -2 * dotAD, 0],
            [4 * (_ra * _rb - _dotAB), 0, 0],
            [_dotAA - _ra * _ra, 0, 0]
        ];

        foreach ((double t, double u) in TubeCurveMath.FindEnvelopeHits(g, 7, tMin, tMax))
        {
            Point point = ray.At(t);
            Point center = Start - 2 * u * _b + u * u * _a;

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
            double dotQpB = qp.Dot(_b);
            double dotQpA = qp.Dot(_a);

            double[] f =
            [
                dotQpQp - StartRadius * StartRadius,
                4 * (StartRadius * _rb + dotQpB),
                -2 * StartRadius * _ra - 4 * _rb * _rb + 4 * _dotBB - 2 * dotQpA,
                4 * (_ra * _rb - _dotAB),
                _dotAA - _ra * _ra
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
