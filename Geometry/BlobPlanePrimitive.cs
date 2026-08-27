using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a plane-shaped blob primitive, using the perpendicular distance from a
/// plane as its characteristic distance.  Where a sphere's influence is a ball and a cylinder's is a
/// tube, a plane's is a slab: an infinite sheet of field, thickest at the plane itself and falling
/// away to nothing at its radius on either side.
/// <para>
/// It is here because the distance from a point to a plane is *linear* along a ray, so its square is
/// the quadratic that everything about a blob depends on.  A point, a line and a plane are the only
/// three elements that hold to that, which is exactly why a blob can be built from spheres,
/// cylinders and planes and not from circles or cones.
/// </para>
/// </summary>
public class BlobPlanePrimitive : IBlobPrimitive
{
    public double Strength { get; }

    public double RadiusSquared { get; }

    private readonly Point _point;
    private readonly Vector _normal;
    private readonly double _radius;

    /// <summary>
    /// This constructs a plane primitive through the given point, facing the given way.
    /// </summary>
    /// <param name="point">A point the plane passes through.</param>
    /// <param name="normal">The direction the plane faces, which need not be a unit vector.</param>
    /// <param name="radius">How far the plane's influence reaches to either side of it.</param>
    /// <param name="strength">The strength of the primitive.</param>
    public BlobPlanePrimitive(Point point, Vector normal, double radius, double strength)
    {
        _point = point;
        _normal = normal.Unit;
        _radius = radius;
        Strength = strength;
        RadiusSquared = radius * radius;
    }

    /// <summary>
    /// This method determines the interval, along the given ray, over which this primitive has any
    /// influence on the field at all -- which is the stretch of the ray lying within the slab.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The entry and exit distances, or <c>null</c>, if the ray never comes within range
    /// of this primitive.</returns>
    public (double Enter, double Exit)? GetBoundingInterval(Ray ray)
    {
        double offset = (ray.Origin - _point).Dot(_normal);
        double slope = ray.Direction.Dot(_normal);

        // A ray running along the slab rather than across it is either inside it for its whole length
        // or outside it for its whole length.  Unbounded is the honest answer, and the sweep through
        // the components copes with it: such a component is simply switched on before everything
        // else and never switched off.
        //
        // **The test is for exactly nought, and treating a merely small slope as nought is a bug I
        // wrote and had to go and find.**  A ray a hair off parallel *does* leave the slab, however
        // far along it has to go to do it, and saying otherwise leaves this component's polynomial
        // in force over stretches of the ray where the plane is nowhere near -- which produces roots
        // tens of units away, in mid air, with the field at nought instead of at the threshold.  A
        // tiny slope needs no special case anyway: dividing by it gives a vast interval, and a vast
        // interval is the true answer.
        if (slope == 0)
        {
            return Math.Abs(offset) < _radius
                ? (double.NegativeInfinity, double.PositiveInfinity)
                : null;
        }

        double first = (-_radius - offset) / slope;
        double second = (_radius - offset) / slope;

        return first < second ? (first, second) : (second, first);
    }

    /// <summary>
    /// This method returns the coefficients of the quadratic, in the ray's own distance parameter,
    /// that gives the square of the distance from the ray to our plane.  The distance itself runs
    /// as <c>offset + slope * t</c>, so its square is that squared.
    /// </summary>
    /// <param name="ray">The ray to evaluate against.</param>
    /// <returns>The T0, T1 and T2 coefficients.</returns>
    public (double T0, double T1, double T2) GetDistanceSquaredCoefficients(Ray ray)
    {
        double offset = (ray.Origin - _point).Dot(_normal);
        double slope = ray.Direction.Dot(_normal);

        return (offset * offset, offset * slope, slope * slope);
    }

    /// <summary>
    /// This method evaluates this primitive's contribution to the field and its gradient at the
    /// given point.
    /// </summary>
    /// <param name="point">The point to evaluate at.</param>
    /// <returns>The density and gradient contributions, or <c>null</c>, if the point is outside
    /// this primitive's influence.</returns>
    public (double Density, Vector Gradient)? EvaluateAt(Point point)
    {
        double distance = (point - _point).Dot(_normal);
        double distanceSquared = distance * distance;

        if (distanceSquared > RadiusSquared)
            return null;

        (double density, double gradientScale) = BlobFieldMath.GetDensityAt(
            Strength, RadiusSquared, distanceSquared);

        // The vector from the plane to the point, which is what every other primitive hands back:
        // the way out of itself, scaled by how fast the field is falling there.
        return (density, _normal * (distance * gradientScale));
    }
}
