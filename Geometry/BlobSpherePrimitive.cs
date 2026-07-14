using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a sphere-shaped blob primitive.  It is also used, with an
/// optional half-space clip, to represent the hemispherical caps at the ends of a cylinder
/// component.
/// </summary>
public class BlobSpherePrimitive : IBlobPrimitive
{
    public double Strength { get; }

    public double RadiusSquared { get; }

    private readonly Point _center;
    private readonly Vector _clipNormal;
    private readonly double _c0;
    private readonly double _c1;
    private readonly double _c2;

    /// <summary>
    /// This constructs a sphere primitive.  When <paramref name="clipNormal"/> is given,
    /// only the half of the sphere the normal points *away* from contributes to the field
    /// -- a point is excluded when <c>(point - center) . clipNormal &gt; 0</c> -- which is
    /// how a cylinder component's rounded end caps are represented.
    /// </summary>
    /// <param name="center">The center of the sphere.</param>
    /// <param name="radius">The radius of the sphere.</param>
    /// <param name="strength">The strength of the primitive.</param>
    /// <param name="clipNormal">An optional half-space clip normal.</param>
    public BlobSpherePrimitive(Point center, double radius, double strength, Vector clipNormal = null)
    {
        _center = center;
        _clipNormal = clipNormal;
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

        return ClipInterval(ray, enter, exit);
    }

    /// <summary>
    /// This method returns the coefficients of the quadratic, in the ray's own distance
    /// parameter <c>t</c>, that gives the square of the distance from the ray to our
    /// center at any point along the ray.
    /// </summary>
    /// <param name="ray">The ray to evaluate against.</param>
    /// <returns>The T0, T1 and T2 coefficients.</returns>
    public (double T0, double T1, double T2) GetDistanceSquaredCoefficients(Ray ray)
    {
        Vector relative = ray.Origin - _center;

        return (relative.Dot(relative), relative.Dot(ray.Direction), ray.Direction.Dot(ray.Direction));
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
        Vector relative = point - _center;
        double distanceSquared = relative.Dot(relative);

        if (distanceSquared > RadiusSquared || (_clipNormal != null && relative.Dot(_clipNormal) > 0))
            return null;

        double density = _c0 * distanceSquared * distanceSquared + _c1 * distanceSquared + _c2;
        double gradientScale = -2.0 * _c0 * distanceSquared - _c1;

        return (density, relative * gradientScale);
    }

    /// <summary>
    /// This method clips the given [enter, exit] interval to whichever half of the sphere
    /// our clip normal allows, if we have one.
    /// </summary>
    /// <param name="ray">The ray the interval was computed for.</param>
    /// <param name="enter">The unclipped entry distance.</param>
    /// <param name="exit">The unclipped exit distance.</param>
    /// <returns>The clipped interval, or <c>null</c>, if the clip removes it entirely.</returns>
    private (double Enter, double Exit)? ClipInterval(Ray ray, double enter, double exit)
    {
        if (_clipNormal == null)
            return (enter, exit);

        Vector relativeOrigin = ray.Origin - _center;
        double a = relativeOrigin.Dot(_clipNormal);
        double b = ray.Direction.Dot(_clipNormal);

        // We keep only where a + b*t <= 0.
        if (b.Near(0))
            return a <= 0 ? (enter, exit) : null;

        double boundary = -a / b;

        if (b > 0)
            exit = Math.Min(exit, boundary);
        else
            enter = Math.Max(enter, boundary);

        return enter < exit ? (enter, exit) : null;
    }
}
