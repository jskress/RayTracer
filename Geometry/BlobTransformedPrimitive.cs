using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class wraps another blob primitive in a transform, so that a component may be stretched,
/// squashed, turned or sheared out of the shape its own maths can describe.  A sphere in a
/// transformed space is an ellipsoid; a cylinder in one is an elliptical or sheared bond.
/// <para>
/// **This works, where a taper or a torus would not, because the transform is affine.**  Everything
/// about a blob rests on one thing: the squared distance from a primitive's characteristic point or
/// line must come out as a *quadratic* in the ray's own distance parameter, since that is what keeps
/// the field a polynomial the solver can take apart.  Carrying a ray through an affine map leaves it
/// a ray with the same parameter along it -- the origin and direction move, but <c>t</c> does not --
/// so the squared distance measured in the component's own space is still a quadratic in the very
/// same <c>t</c>.  Nothing downstream can tell the difference.  A cone, by contrast, is not an
/// affine image of a cylinder, and its varying radius makes the falloff a ratio of quadratics rather
/// than a polynomial, which is why a tapered component cannot be had this cheaply.
/// </para>
/// </summary>
public class BlobTransformedPrimitive : IBlobPrimitive
{
    public double Strength => _primitive.Strength;

    public double RadiusSquared => _primitive.RadiusSquared;

    private readonly IBlobPrimitive _primitive;
    private readonly Matrix _inverse;
    private readonly Matrix _inverseTransposed;

    /// <summary>
    /// This constructs a primitive that is the given one, seen through the given transform.
    /// </summary>
    /// <param name="primitive">The primitive to transform.</param>
    /// <param name="transform">The transform to see it through.</param>
    public BlobTransformedPrimitive(IBlobPrimitive primitive, Matrix transform)
    {
        _primitive = primitive;
        _inverse = transform.Invert();
        _inverseTransposed = _inverse.Transpose();
    }

    /// <summary>
    /// This method determines the interval, along the given ray, over which this primitive has any
    /// influence on the field at all.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The entry and exit distances, or <c>null</c>, if the ray never comes within range
    /// of this primitive.</returns>
    public (double Enter, double Exit)? GetBoundingInterval(Ray ray)
    {
        return _primitive.GetBoundingInterval(ToComponentSpace(ray));
    }

    /// <summary>
    /// This method returns the coefficients of the quadratic that gives the square of this
    /// primitive's characteristic distance along the ray.
    /// </summary>
    /// <param name="ray">The ray to evaluate against.</param>
    /// <returns>The T0, T1 and T2 coefficients.</returns>
    public (double T0, double T1, double T2) GetDistanceSquaredCoefficients(Ray ray)
    {
        return _primitive.GetDistanceSquaredCoefficients(ToComponentSpace(ray));
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
        (double Density, Vector Gradient)? result = _primitive.EvaluateAt(_inverse * point);

        // The density is a number and comes back as it is, but the gradient is a normal and must be
        // carried the other way about -- through the inverse *transposed* -- or a squashed component
        // would shade as though it were still round.
        return result is null
            ? null
            : (result.Value.Density, _inverseTransposed * result.Value.Gradient);
    }

    /// <summary>
    /// This method carries a ray into the space the wrapped primitive is described in.  The
    /// direction is deliberately left unnormalized: the point of it is that a distance along the
    /// transformed ray means the same as the same distance along the original.
    /// </summary>
    /// <param name="ray">The ray to carry over.</param>
    /// <returns>The ray, in the component's own space.</returns>
    private Ray ToComponentSpace(Ray ray)
    {
        return new Ray(_inverse * ray.Origin, _inverse * ray.Direction, ray.TimeIndex);
    }
}
