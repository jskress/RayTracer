using MathNet.Numerics;
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using Complex = System.Numerics.Complex;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents an egg (POV-Ray calls this shape an "ovus"): two spheres -- a
/// larger one at the bottom, a smaller one at the top -- smoothly joined by a connecting
/// collar, forming a classic egg silhouette when revolved around the Y axis.  Ported from
/// POV-Ray's own ovus primitive (<c>source/backend/shape/ovus.cpp</c>'s
/// <c>Intersect_Ovus_Spheres</c>/<c>All_Intersections</c>/<c>Normal</c>, and the precompute
/// step in <c>source/backend/parser/parse.cpp</c>'s <c>Parse_Ovus</c>).
/// </summary>
public class Egg : Surface
{
    /// <summary>
    /// This property provides the radius of the egg's bottom sphere, which is centered at
    /// the origin.
    /// </summary>
    public double BottomRadius { get; set; }

    /// <summary>
    /// This property provides the radius of the egg's top sphere, which is centered at
    /// <c>(0, BottomRadius, 0)</c> -- the bottom sphere's own "north pole".
    /// </summary>
    public double TopRadius { get; set; }

    // These are all precomputed once, in PrepareSurfaceForRendering, from BottomRadius and
    // TopRadius -- see that method for the derivation, ported directly from POV-Ray's own
    // Parse_Ovus.  Together, they describe the connecting collar: a piece of a deliberately
    // self-intersecting ("spindle") torus of major radius _horizontalPosition and minor
    // radius _connectingRadius, centered at (0, _verticalPosition, 0), clipped to the Y
    // range (_bottomVertical, _topVertical).
    private double _horizontalPosition;
    private double _verticalPosition;
    private double _bottomVertical;
    private double _topVertical;
    private double _connectingRadius;
    private double _majorSquared;
    private double _minorSquared;

    /// <summary>
    /// This method precomputes the values that stay constant for the egg, regardless of the
    /// ray being tested -- the connecting collar's geometry, derived from the two radii
    /// exactly as POV-Ray's <c>Parse_Ovus</c> does.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (BottomRadius <= 0 || TopRadius <= 0)
            throw new Exception("An egg's radii must both be positive.");

        if (TopRadius >= 2 * BottomRadius)
        {
            throw new Exception(
                "An egg's top radius must be less than twice its bottom radius -- otherwise " +
                "the top sphere would swallow the bottom one whole, leaving nothing egg-shaped " +
                "to connect.");
        }

        if (BottomRadius > TopRadius)
        {
            _connectingRadius = 2 * BottomRadius;
            _verticalPosition = 2 * TopRadius - BottomRadius - TopRadius * TopRadius / (2 * BottomRadius);
            _horizontalPosition = Math.Sqrt(BottomRadius * BottomRadius - _verticalPosition * _verticalPosition);
            _bottomVertical = -_verticalPosition;

            double distance = _connectingRadius - TopRadius;

            _topVertical = (BottomRadius - _verticalPosition) * TopRadius / distance + BottomRadius;
        }
        else
        {
            _connectingRadius = 2 * TopRadius;
            _verticalPosition = -2 * TopRadius + BottomRadius + 1.5 * TopRadius * TopRadius / BottomRadius;
            _horizontalPosition = Math.Sqrt(
                TopRadius * TopRadius - (_verticalPosition - BottomRadius) * (_verticalPosition - BottomRadius));
            _topVertical = 2 * BottomRadius - _verticalPosition;

            double distance = _connectingRadius - BottomRadius;

            _bottomVertical = -_verticalPosition * BottomRadius / distance;
        }

        _majorSquared = _horizontalPosition * _horizontalPosition;
        _minorSquared = _connectingRadius * _connectingRadius;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the egg and, if
    /// so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        AddCapIntersections(ray, Point.Zero, BottomRadius, intersections, below: true);
        AddCapIntersections(ray, new Point(0, BottomRadius, 0), TopRadius, intersections, below: false);
        AddConnectingSurfaceIntersections(ray, intersections);
    }

    /// <summary>
    /// This method solves for, and adds, any intersections with one of the egg's two sphere
    /// caps, keeping only the points that lie past the connecting collar's boundary (rather
    /// than the part of the sphere the collar itself covers).
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="center">The center of the sphere to test.</param>
    /// <param name="radius">The radius of the sphere to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    /// <param name="below">Whether accepted points must lie below (the bottom sphere) or
    /// above (the top sphere) the collar.</param>
    private void AddCapIntersections(
        Ray ray, Point center, double radius, List<Intersection> intersections, bool below)
    {
        Vector sphereToRay = ray.Origin - center;
        double a = ray.Direction.Dot(ray.Direction);
        double b = 2 * ray.Direction.Dot(sphereToRay);
        double c = sphereToRay.Dot(sphereToRay) - radius * radius;

        foreach (double t in TubeCurveMath.SolveQuadratic(a, b, c))
        {
            Point point = ray.At(t);
            bool accepted = below ? point.Y < _bottomVertical : point.Y > _topVertical;

            if (accepted)
                intersections.Add(new Intersection(this, t));
        }
    }

    /// <summary>
    /// This method solves for, and adds, any intersections with the egg's connecting
    /// collar -- the same quartic <see cref="Torus"/> solves for a normal torus, just
    /// shifted to be centered at the collar's own Y position, and additionally filtered to
    /// keep only the concave "lemon" lobe of this deliberately self-intersecting torus (the
    /// only lobe that's actually part of the egg), the way POV-Ray's own
    /// <c>Intersect_Ovus_Spheres</c> does.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void AddConnectingSurfaceIntersections(Ray ray, List<Intersection> intersections)
    {
        double length = ray.Direction.Magnitude;
        Vector direction = ray.Direction.Unit;
        double ox = ray.Origin.X;
        double oy = ray.Origin.Y - _verticalPosition;
        double oz = ray.Origin.Z;
        double dx = direction.X;
        double dy = direction.Y;
        double dz = direction.Z;
        double oySquared = oy * oy;
        double dySquared = dy * dy;
        double crossY = oy * dy;
        double k1 = ox * ox + oz * oz + oySquared - _majorSquared - _minorSquared;
        double k2 = ox * dx + oz * dz + crossY;

        // Coefficients are given in ascending order (constant term first) to match the
        // convention MathNet.Numerics.Polynomial expects.
        double[] coefficients =
        [
            k1 * k1 + 4.0 * _majorSquared * (oySquared - _minorSquared),
            4.0 * (k2 * k1 + 2.0 * _majorSquared * crossY),
            2.0 * (k1 + 2.0 * (k2 * k2 + _majorSquared * dySquared)),
            4.0 * k2,
            1.0
        ];
        Polynomial polynomial = new (coefficients);

        foreach (Complex root in polynomial.Roots())
        {
            if (!root.Imaginary.Near(0))
                continue;

            double t = root.Real / length;
            Point point = ray.At(t);

            if (point.Y <= _bottomVertical || point.Y >= _topVertical)
                continue;

            double horizontal = Math.Sqrt(point.X * point.X + point.Z * point.Z);
            double lemonDistanceSquared =
                (horizontal + _horizontalPosition) * (horizontal + _horizontalPosition) +
                (point.Y - _verticalPosition) * (point.Y - _verticalPosition);

            if (lemonDistanceSquared.Near(_minorSquared, 0.0001))
                intersections.Add(new Intersection(this, t));
        }
    }

    /// <summary>
    /// This method returns the normal for the egg.  It is assumed that the point will have
    /// been transformed to surface-space coordinates.  The vector returned will also be in
    /// surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        if (point.Y < _bottomVertical)
            return (point - Point.Zero).Unit;

        if (point.Y > _topVertical)
            return (point - new Point(0, BottomRadius, 0)).Unit;

        double x = point.X;
        double y = point.Y - _verticalPosition;
        double z = point.Z;
        double distance = Math.Sqrt(x * x + z * z);
        Vector outward = distance.Near(0)
            ? new Vector(0, 0, 0)
            : new Vector(_horizontalPosition * x / distance, 0, _horizontalPosition * z / distance);

        return (new Vector(x, y, z) + outward).Unit;
    }
}
