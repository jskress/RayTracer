using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This interface represents a single low-level shape (a sphere, or one piece of a
/// cylinder) that contributes one quartic term to a blob's field along any given ray.  The
/// field this primitive contributes, as a function of the squared distance from its
/// characteristic point (a sphere's center) or line (a cylinder's axis), is the classic
/// metaball falloff:  density(d^2) = strength * (1 - d^2 / R^2)^2 for d &lt;= R, and zero
/// beyond it.
/// </summary>
public interface IBlobPrimitive
{
    /// <summary>
    /// This property holds the strength of the primitive.
    /// </summary>
    double Strength { get; }

    /// <summary>
    /// This property holds the square of the primitive's influence radius.  Points farther
    /// than this from the primitive contribute nothing to the field.
    /// </summary>
    double RadiusSquared { get; }

    /// <summary>
    /// This method determines the interval, along the given ray, over which this primitive
    /// has any influence on the field at all.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The entry and exit distances, or <c>null</c>, if the ray never comes within
    /// range of this primitive.</returns>
    (double Enter, double Exit)? GetBoundingInterval(Ray ray);

    /// <summary>
    /// This method returns the coefficients of the quadratic, in the ray's own distance
    /// parameter <c>t</c>, that gives the square of this primitive's characteristic
    /// distance (from a sphere's center, or from a cylinder's axis) at any point along the
    /// ray:  distanceSquared(t) = T0 + 2 * T1 * t + T2 * t^2.
    /// </summary>
    /// <param name="ray">The ray to evaluate against.</param>
    /// <returns>The T0, T1 and T2 coefficients.</returns>
    (double T0, double T1, double T2) GetDistanceSquaredCoefficients(Ray ray);

    /// <summary>
    /// This method evaluates this primitive's contribution to the field and its gradient at
    /// the given point.
    /// </summary>
    /// <param name="point">The point to evaluate at.</param>
    /// <returns>The density and gradient contributions, or <c>null</c>, if the point is
    /// outside this primitive's influence.</returns>
    (double Density, Vector Gradient)? EvaluateAt(Point point);
}
