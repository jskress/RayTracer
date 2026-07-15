using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a cubic Bezier piece of a <see cref="Spline"/>.
/// </summary>
public class SplineCubicCurve : ISplineCurve
{
    /// <summary>
    /// This property holds the point the curve starts at.
    /// </summary>
    public Point Start { get; set; }

    /// <summary>
    /// This property holds the curve's first control point.
    /// </summary>
    public Point Control1 { get; set; }

    /// <summary>
    /// This property holds the curve's second control point.
    /// </summary>
    public Point Control2 { get; set; }

    /// <summary>
    /// This property holds the point the curve ends at.
    /// </summary>
    public Point End { get; set; }

    /// <summary>
    /// This method returns the point on the curve at the given parameter.  This uses the
    /// same power-basis form (<c>Start + K1*u + K2*u^2 + K3*u^3</c>) as
    /// <see cref="TubeCubicSegment"/>.
    /// </summary>
    /// <param name="u">The parameter to evaluate at, from 0 (the curve's start) to 1 (its
    /// end).</param>
    /// <returns>The point at that parameter.</returns>
    public Point GetPoint(double u)
    {
        (Vector k1, Vector k2, Vector k3) = GetCoefficients();

        return Start + u * k1 + u * u * k2 + u * u * u * k3;
    }

    /// <summary>
    /// This method returns the curve's direction of travel at the given parameter.
    /// </summary>
    /// <param name="u">The parameter to evaluate at, from 0 (the curve's start) to 1 (its
    /// end).</param>
    /// <returns>The tangent direction at that parameter.</returns>
    public Vector GetTangent(double u)
    {
        (Vector k1, Vector k2, Vector k3) = GetCoefficients();

        return k1 + 2 * u * k2 + 3 * u * u * k3;
    }

    /// <summary>
    /// This method computes the power-basis coefficients for our cubic Bezier.
    /// </summary>
    /// <returns>The K1, K2 and K3 coefficients.</returns>
    private (Vector K1, Vector K2, Vector K3) GetCoefficients()
    {
        Vector k1 = 3 * (Control1 - Start);
        Vector k2 = 3 * ((Start - Control1) + (Control2 - Control1));
        Vector k3 = (End - Start) - 3 * (Control2 - Control1);

        return (k1, k2, k3);
    }
}
