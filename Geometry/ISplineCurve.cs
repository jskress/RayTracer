using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This interface is implemented by the individual pieces (straight, quadratic or cubic)
/// that make up a <see cref="Spline"/>, each able to evaluate its own position and
/// direction of travel at a parameter in <c>[0, 1]</c>.
/// </summary>
public interface ISplineCurve
{
    /// <summary>
    /// This method returns the point on the curve at the given parameter.
    /// </summary>
    /// <param name="u">The parameter to evaluate at, from 0 (the curve's start) to 1 (its
    /// end).</param>
    /// <returns>The point at that parameter.</returns>
    Point GetPoint(double u);

    /// <summary>
    /// This method returns the curve's direction of travel (its derivative, not
    /// necessarily of unit length) at the given parameter.
    /// </summary>
    /// <param name="u">The parameter to evaluate at, from 0 (the curve's start) to 1 (its
    /// end).</param>
    /// <returns>The tangent direction at that parameter.</returns>
    Vector GetTangent(double u);
}
