using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a quadratic Bezier piece of a <see cref="Spline"/>.
/// </summary>
public class SplineQuadCurve : ISplineCurve
{
    /// <summary>
    /// This property holds the point the curve starts at.
    /// </summary>
    public Point Start { get; set; }

    /// <summary>
    /// This property holds the curve's control point.
    /// </summary>
    public Point Control { get; set; }

    /// <summary>
    /// This property holds the point the curve ends at.
    /// </summary>
    public Point End { get; set; }

    /// <summary>
    /// This method returns the point on the curve at the given parameter.  This uses the
    /// same power-basis form (<c>Start - 2u*B + u^2*A</c>) as <see cref="TubeQuadSegment"/>.
    /// </summary>
    /// <param name="u">The parameter to evaluate at, from 0 (the curve's start) to 1 (its
    /// end).</param>
    /// <returns>The point at that parameter.</returns>
    public Point GetPoint(double u)
    {
        Vector b = Start - Control;
        Vector a = b - (Control - End);

        return Start - 2 * u * b + u * u * a;
    }

    /// <summary>
    /// This method returns the curve's direction of travel at the given parameter.
    /// </summary>
    /// <param name="u">The parameter to evaluate at, from 0 (the curve's start) to 1 (its
    /// end).</param>
    /// <returns>The tangent direction at that parameter.</returns>
    public Vector GetTangent(double u)
    {
        Vector b = Start - Control;
        Vector a = b - (Control - End);

        return -2 * b + 2 * u * a;
    }
}
