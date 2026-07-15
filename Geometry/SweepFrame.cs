using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents one orientation frame along a swept path: a position, the path's
/// direction of travel there, and two axes perpendicular to it (spanning the plane a
/// profile gets lofted into at that point).
/// </summary>
public class SweepFrame
{
    /// <summary>
    /// This property holds the frame's position.
    /// </summary>
    public Point Position { get; init; }

    /// <summary>
    /// This property holds the (unit) direction of travel at this frame.
    /// </summary>
    public Vector Tangent { get; init; }

    /// <summary>
    /// This property holds one of the two axes perpendicular to the tangent.
    /// </summary>
    public Vector Normal { get; init; }

    /// <summary>
    /// This property holds the other axis perpendicular to the tangent, completing a
    /// right-handed frame with <see cref="Tangent"/> and <see cref="Normal"/>.
    /// </summary>
    public Vector Binormal { get; init; }
}
