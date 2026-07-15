using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a straight-line piece of a <see cref="Spline"/>.
/// </summary>
public class SplineLine : ISplineCurve
{
    /// <summary>
    /// This property holds the point the line starts at.
    /// </summary>
    public Point Start { get; set; }

    /// <summary>
    /// This property holds the point the line ends at.
    /// </summary>
    public Point End { get; set; }

    /// <summary>
    /// This method returns the point on the line at the given parameter.
    /// </summary>
    /// <param name="u">The parameter to evaluate at, from 0 (the line's start) to 1 (its
    /// end).</param>
    /// <returns>The point at that parameter.</returns>
    public Point GetPoint(double u)
    {
        return Start + (End - Start) * u;
    }

    /// <summary>
    /// This method returns the line's constant direction of travel.
    /// </summary>
    /// <param name="u">Unused; a line's direction doesn't depend on where along it we
    /// are.</param>
    /// <returns>The tangent direction.</returns>
    public Vector GetTangent(double u)
    {
        return End - Start;
    }
}
