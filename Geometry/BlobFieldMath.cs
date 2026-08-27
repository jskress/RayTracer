namespace RayTracer.Geometry;

/// <summary>
/// This class provides math shared by all blob primitives for turning a "distance squared
/// as a quadratic in the ray's t" relationship into the polynomial (in ascending order)
/// that describes a primitive's field contribution along a ray.
/// </summary>
internal static class BlobFieldMath
{
    /// <summary>
    /// This method computes the density coefficients for a primitive of the given strength
    /// and influence radius, as a cubic in the squared distance:
    /// density(d^2) = D0 * (d^2)^3 + D1 * (d^2)^2 + D2 * d^2 + D3.
    /// <para>
    /// The falloff is <c>strength * (1 - d^2 / R^2)^3</c>.
    /// </para>
    /// <para>
    /// **The cube is what keeps a joint from showing a hip, and the square did not.**  The classic
    /// metaball falloff is the *square* of that bracket, which reaches the influence radius with
    /// both its value and its slope at nothing -- so the surface is smooth there and its normal is
    /// continuous.  What it does not bring to nothing is its second derivative, and where one
    /// component's influence ends part way across another, that step in curvature lands on the
    /// visible surface.  The eye reads a step in curvature as a line, much as it reads a Mach band,
    /// and no arrangement of radius and strength removes it: it can only be moved somewhere else.
    /// </para>
    /// <para>
    /// Cubing the bracket puts a triple root at the influence radius, so value, slope *and*
    /// curvature all arrive at nothing together.  A cubic is the lowest order that can: asking for
    /// three zeros at one place is asking for a triple root, and a quadratic has only two to give.
    /// </para>
    /// <para>
    /// It costs a degree.  Density was quadratic in the squared distance and is now cubic, and the
    /// squared distance is itself quadratic in the ray's own parameter -- so the field along a ray
    /// goes from a quartic to a sextic, and its roots have to be found rather than written down.
    /// </para>
    /// </summary>
    /// <param name="strength">The primitive's strength.</param>
    /// <param name="radiusSquared">The square of the primitive's influence radius.</param>
    /// <returns>The D0, D1, D2 and D3 density coefficients.</returns>
    internal static (double D0, double D1, double D2, double D3) GetDensityCoefficients(
        double strength, double radiusSquared)
    {
        double squared = radiusSquared * radiusSquared;

        return (
            -strength / (squared * radiusSquared),
            3.0 * strength / squared,
            -3.0 * strength / radiusSquared,
            strength);
    }

    /// <summary>
    /// This method reports the density, and the rate it changes with the squared distance, at a
    /// given squared distance.  It is what a primitive asked about one point needs, where the
    /// polynomial below is what a primitive asked about a whole ray needs.
    /// </summary>
    /// <param name="strength">The primitive's strength.</param>
    /// <param name="radiusSquared">The square of the primitive's influence radius.</param>
    /// <param name="distanceSquared">How far from the primitive the point is, squared.</param>
    /// <returns>The density there, and the factor a gradient is scaled by.</returns>
    internal static (double Density, double GradientScale) GetDensityAt(
        double strength, double radiusSquared, double distanceSquared)
    {
        double falloff = 1.0 - distanceSquared / radiusSquared;

        // The gradient of the density with respect to the point is the derivative of the density
        // with respect to the squared distance, times the vector out from the primitive.  The sign
        // is turned about and the factor of two dropped, which is the convention every primitive
        // here shares -- what matters is that they all agree, since their gradients are summed
        // before anything is normalized.
        return (strength * falloff * falloff * falloff,
                3.0 * strength * falloff * falloff / radiusSquared);
    }

    /// <summary>
    /// This method expands a primitive's "distance squared as a quadratic in t" relation
    /// and its density coefficients into the polynomial, in ascending order, that gives its
    /// field contribution as a function of the ray's distance parameter t.
    /// </summary>
    /// <param name="t0">The constant term of distanceSquared(t).</param>
    /// <param name="t1">Half the linear coefficient of distanceSquared(t).</param>
    /// <param name="t2">The quadratic coefficient of distanceSquared(t).</param>
    /// <param name="d0">The density coefficient for (d^2)^3.</param>
    /// <param name="d1">The density coefficient for (d^2)^2.</param>
    /// <param name="d2">The density coefficient for d^2.</param>
    /// <param name="d3">The density coefficient for the constant term.</param>
    /// <returns>The seven field polynomial coefficients, in ascending order.</returns>
    internal static double[] GetFieldPolynomial(
        double t0, double t1, double t2, double d0, double d1, double d2, double d3)
    {
        // The squared distance, written out as `a t^2 + b t + c`, so that cubing it is ordinary
        // algebra rather than something to be squinted at.
        double a = t2;
        double b = 2.0 * t1;
        double c = t0;

        return
        [
            d0 * c * c * c + d1 * c * c + d2 * c + d3,
            d0 * 3.0 * b * c * c + d1 * 2.0 * b * c + d2 * b,
            d0 * (3.0 * b * b * c + 3.0 * a * c * c) + d1 * (b * b + 2.0 * a * c) + d2 * a,
            d0 * (b * b * b + 6.0 * a * b * c) + d1 * 2.0 * a * b,
            d0 * (3.0 * a * a * c + 3.0 * a * b * b) + d1 * a * a,
            d0 * 3.0 * a * a * b,
            d0 * a * a * a
        ];
    }
}
