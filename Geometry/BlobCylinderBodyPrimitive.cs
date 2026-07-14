using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents the cylindrical body of a cylinder blob component -- the portion
/// between its two end caps, using perpendicular distance to the axis as its characteristic
/// distance.  The two hemispherical caps are handled separately, by
/// <see cref="BlobSpherePrimitive"/>.
/// </summary>
public class BlobCylinderBodyPrimitive : IBlobPrimitive
{
    public double Strength { get; }

    public double RadiusSquared { get; }

    private readonly Point _start;
    private readonly Vector _axisUnit;
    private readonly double _axisLength;
    private readonly double _c0;
    private readonly double _c1;
    private readonly double _c2;

    /// <summary>
    /// This constructs a cylinder body primitive spanning from <paramref name="start"/> to
    /// <paramref name="end"/>.
    /// </summary>
    /// <param name="start">The center of the cylinder's base.</param>
    /// <param name="end">The center of the cylinder's apex.</param>
    /// <param name="radius">The radius of the cylinder.</param>
    /// <param name="strength">The strength of the primitive.</param>
    public BlobCylinderBodyPrimitive(Point start, Point end, double radius, double strength)
    {
        Vector axis = end - start;

        _start = start;
        _axisLength = axis.Magnitude;
        _axisUnit = axis.Unit;
        Strength = strength;
        RadiusSquared = radius * radius;

        (_c0, _c1, _c2) = BlobFieldMath.GetDensityCoefficients(strength, RadiusSquared);
    }

    /// <summary>
    /// This method determines the interval, along the given ray, over which this primitive
    /// has any influence on the field at all.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The entry and exit distances, or <c>null</c>, if the ray never comes within
    /// range of this primitive.</returns>
    public (double Enter, double Exit)? GetBoundingInterval(Ray ray)
    {
        (double t0, double t1, double t2) = GetDistanceSquaredCoefficients(ray);

        if (t2.Near(0))
            return null;

        double discriminant = t1 * t1 - t2 * (t0 - RadiusSquared);

        if (discriminant < 0)
            return null;

        double sqrtDiscriminant = Math.Sqrt(discriminant);
        double enter = (-t1 - sqrtDiscriminant) / t2;
        double exit = (-t1 + sqrtDiscriminant) / t2;

        // Clip to the axial range [0, axisLength].
        double originAxial = (ray.Origin - _start).Dot(_axisUnit);
        double directionAxial = ray.Direction.Dot(_axisUnit);

        if (directionAxial.Near(0))
            return originAxial >= 0 && originAxial <= _axisLength ? (enter, exit) : null;

        double axialEnter = -originAxial / directionAxial;
        double axialExit = (_axisLength - originAxial) / directionAxial;

        if (axialEnter > axialExit)
            (axialEnter, axialExit) = (axialExit, axialEnter);

        enter = Math.Max(enter, axialEnter);
        exit = Math.Min(exit, axialExit);

        return enter < exit ? (enter, exit) : null;
    }

    /// <summary>
    /// This method returns the coefficients of the quadratic, in the ray's own distance
    /// parameter <c>t</c>, that gives the square of the perpendicular distance from the ray
    /// to our axis at any point along the ray.
    /// </summary>
    /// <param name="ray">The ray to evaluate against.</param>
    /// <returns>The T0, T1 and T2 coefficients.</returns>
    public (double T0, double T1, double T2) GetDistanceSquaredCoefficients(Ray ray)
    {
        Vector relativeOrigin = ray.Origin - _start;
        double originAxial = relativeOrigin.Dot(_axisUnit);
        double directionAxial = ray.Direction.Dot(_axisUnit);
        double t0 = relativeOrigin.Dot(relativeOrigin) - originAxial * originAxial;
        double t1 = relativeOrigin.Dot(ray.Direction) - originAxial * directionAxial;
        double t2 = ray.Direction.Dot(ray.Direction) - directionAxial * directionAxial;

        return (t0, t1, t2);
    }

    /// <summary>
    /// This method evaluates this primitive's contribution to the field and its gradient at
    /// the given point.
    /// </summary>
    /// <param name="point">The point to evaluate at.</param>
    /// <returns>The density and gradient contributions, or <c>null</c>, if the point is
    /// outside this primitive's influence.</returns>
    public (double Density, Vector Gradient)? EvaluateAt(Point point)
    {
        Vector relative = point - _start;
        double axial = relative.Dot(_axisUnit);

        if (axial < 0 || axial > _axisLength)
            return null;

        Vector perpendicular = relative - _axisUnit * axial;
        double distanceSquared = perpendicular.Dot(perpendicular);

        if (distanceSquared > RadiusSquared)
            return null;

        double density = _c0 * distanceSquared * distanceSquared + _c1 * distanceSquared + _c2;
        double gradientScale = -2.0 * _c0 * distanceSquared - _c1;

        return (density, perpendicular * gradientScale);
    }
}
