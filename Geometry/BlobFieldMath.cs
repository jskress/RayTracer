namespace RayTracer.Geometry;

/// <summary>
/// This class provides math shared by all blob primitives for turning a "distance squared
/// as a quadratic in the ray's t" relationship into the quartic polynomial (in ascending
/// order) that describes a primitive's field contribution along a ray.
/// </summary>
internal static class BlobFieldMath
{
    /// <summary>
    /// This method computes the density coefficients (C0, C1, C2 in
    /// density(d^2) = C0 * (d^2)^2 + C1 * d^2 + C2) for a primitive of the given strength
    /// and influence radius.  This is the classic metaball falloff:
    /// density(d^2) = strength * (1 - d^2 / R^2)^2.
    /// </summary>
    /// <param name="strength">The primitive's strength.</param>
    /// <param name="radiusSquared">The square of the primitive's influence radius.</param>
    /// <returns>The C0, C1 and C2 density coefficients.</returns>
    internal static (double C0, double C1, double C2) GetDensityCoefficients(
        double strength, double radiusSquared)
    {
        double c0 = strength / (radiusSquared * radiusSquared);
        double c1 = -2.0 * strength / radiusSquared;

        return (c0, c1, strength);
    }

    /// <summary>
    /// This method expands a primitive's "distance squared as a quadratic in t" relation
    /// and its density coefficients into the quartic polynomial, in ascending order, that
    /// gives its field contribution as a function of the ray's distance parameter t.
    /// </summary>
    /// <param name="t0">The constant term of distanceSquared(t).</param>
    /// <param name="t1">Half the linear coefficient of distanceSquared(t).</param>
    /// <param name="t2">The quadratic coefficient of distanceSquared(t).</param>
    /// <param name="c0">The density coefficient for (d^2)^2.</param>
    /// <param name="c1">The density coefficient for d^2.</param>
    /// <param name="c2">The density coefficient for the constant term.</param>
    /// <returns>The five field polynomial coefficients, in ascending order.</returns>
    internal static double[] GetFieldPolynomial(
        double t0, double t1, double t2, double c0, double c1, double c2)
    {
        return
        [
            c0 * t0 * t0 + c1 * t0 + c2,
            2.0 * t1 * (2.0 * c0 * t0 + c1),
            2.0 * c0 * (2.0 * t1 * t1 + t0 * t2) + c1 * t2,
            4.0 * c0 * t1 * t2,
            c0 * t2 * t2
        ];
    }
}
