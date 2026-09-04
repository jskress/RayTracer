using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a general quadric: the surface where
/// <c>Ax² + By² + Cz² + Dxy + Exz + Fyz + Gx + Hy + Iz + J = 0</c>.
/// <para>
/// **This is the escape hatch, not the front door.**  Ten numbers with no picture attached is a poor
/// way to ask for a shape, and the named forms -- <see cref="Paraboloid"/>,
/// <see cref="Hyperboloid"/>, <see cref="Saddle"/>, and the sphere, cylinder and conic that came
/// long before them -- are what a scene should reach for.  What this adds is the rest of the family:
/// the two-sheet hyperboloid, an elliptic or hyperbolic cylinder, a cone about an axis of its own,
/// a pair of planes.
/// </para>
/// <para>
/// **It is a solid**, inside wherever the equation comes out below nought, which is the reading that
/// makes a sphere's inside its middle and a saddle's inside the half of space beneath it.  That is
/// also what lets it stand in a CSG.
/// </para>
/// <para>
/// **A box is required**, as it is for an <see cref="Isosurface"/> and for the same reason: most
/// quadrics are endless, and nothing about ten coefficients says where to stop looking.  The box is
/// the region the surface is taken to exist in, not a hint.
/// </para>
/// </summary>
public class Quadric : Surface
{
    /// <summary>The coefficient of x².</summary>
    public double A { get; set; }

    /// <summary>The coefficient of y².</summary>
    public double B { get; set; }

    /// <summary>The coefficient of z².</summary>
    public double C { get; set; }

    /// <summary>The coefficient of xy.</summary>
    public double D { get; set; }

    /// <summary>The coefficient of xz.</summary>
    public double E { get; set; }

    /// <summary>The coefficient of yz.</summary>
    public double F { get; set; }

    /// <summary>The coefficient of x.</summary>
    public double G { get; set; }

    /// <summary>The coefficient of y.</summary>
    public double H { get; set; }

    /// <summary>The coefficient of z.</summary>
    public double I { get; set; }

    /// <summary>The constant term.</summary>
    public double J { get; set; }

    /// <summary>
    /// This method finds where a ray crosses the surface.
    /// <para>
    /// Substituting the ray into the equation leaves a quadratic in the distance along it, so the
    /// crossings are had exactly rather than searched for.  The squared term vanishing is not a
    /// failure: it means the ray meets the surface once, and which rays those are depends on the
    /// coefficients -- down a paraboloid's axis, along a hyperboloid's asymptote, along a saddle's
    /// ruling.  Every one of those is a real crossing and is kept.
    /// </para>
    /// <para>
    /// Crossings behind the ray's origin are reported, as every surface here must: a CSG reads the
    /// sorted crossings from far behind the origin forward to work out what is solid, and refraction
    /// needs them to tell leaving from entering.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        (double ox, double oy, double oz) = (ray.Origin.X, ray.Origin.Y, ray.Origin.Z);
        (double dx, double dy, double dz) = (ray.Direction.X, ray.Direction.Y, ray.Direction.Z);

        double a = A * dx * dx + B * dy * dy + C * dz * dz +
                   D * dx * dy + E * dx * dz + F * dy * dz;
        double b = 2 * (A * ox * dx + B * oy * dy + C * oz * dz) +
                   D * (ox * dy + oy * dx) + E * (ox * dz + oz * dx) + F * (oy * dz + oz * dy) +
                   G * dx + H * dy + I * dz;
        double c = A * ox * ox + B * oy * oy + C * oz * oz +
                   D * ox * oy + E * ox * oz + F * oy * oz +
                   G * ox + H * oy + I * oz + J;
        double directionSquared = ray.Direction.Dot(ray.Direction);

        if (a.IsNegligibleSquaredBeside(directionSquared))
        {
            if (!(b * b).IsNegligibleSquaredBeside(directionSquared))
                intersections.Add(new Intersection(this, -c / b));

            return;
        }

        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return;

        double root = Math.Sqrt(discriminant);

        intersections.Add(new Intersection(this, (-b - root) / (2 * a)));
        intersections.Add(new Intersection(this, (-b + root) / (2 * a)));
    }

    /// <summary>
    /// This method returns the normal, which is the slope of the equation itself.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        (double x, double y, double z) = (point.X, point.Y, point.Z);

        return new Vector(
            2 * A * x + D * y + E * z + G,
            2 * B * y + D * x + F * z + H,
            2 * C * z + E * x + F * y + I);
    }
}
