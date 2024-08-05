using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a torus.  It is defined by major and minor radii.
/// </summary>
public class Torus : Surface
{
    /// <summary>
    /// This property provides the major radius of the torus.
    /// </summary>
    public double MajorRadius { get; }

    /// <summary>
    /// This property provides the minor radius of the torus.
    /// </summary>
    public double MinorRadius { get; }

    private readonly double _majorSquared;
    private readonly double _minorSquared;

    public Torus()
    {
        // This constructor is present to satisfy the type system but should never
        // be used, so...
        throw new Exception("Internal error: cannot create torii this way.");
    }

    public Torus(double majorRadius, double minorRadius)
    {
        _majorSquared = majorRadius * majorRadius;
        _minorSquared = minorRadius * minorRadius;

        MajorRadius = majorRadius;
        MinorRadius = minorRadius;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the torus and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double length = ray.Direction.Magnitude;
        double ox = ray.Origin.X;
        double oy = ray.Origin.Y;
        double oz = ray.Origin.Z;
        double dx = ray.Direction.X;
        double dy = ray.Direction.Y;
        double dz = ray.Direction.Z;
        double oySquared = oy * oy;
        double dySquared = dy * dy;
        double crossY = oy * dy;
        double k1 = ox * ox + oz * oz + oySquared - _majorSquared - _minorSquared;
        double k2 = ox * dx + oz * dz + crossY;
        double[] coefficients = [
            1.0,
            4.0 * k2,
            2.0 * (k1 + 2.0 * (k2 * k2 + _majorSquared * dySquared)),
            4.0 * (k2 * k1 + 2.0 * _majorSquared * crossY),
            k1 * k1 + 4.0 * _majorSquared * (oySquared - _minorSquared)
        ];
        double[] distances = Polynomials.Solve(coefficients);

        if (distances != null)
        {
            intersections.AddRange(distances.Reverse()
                .Select(distance => new Intersection(this, distance / length)));
        }
    }

    /// <summary>
    /// This method returns the normal for the torus.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        double x = point.X;
        double z = point.Z;
        double distance = Math.Sqrt(x * x + z * z);
        Vector vector = distance.Near(0)
            ? new Vector(0, 0, 0)
            : new Vector(
                MajorRadius * x / distance, 0,
                MajorRadius * z / distance);

        return new Vector(point) - vector;
    }
}
