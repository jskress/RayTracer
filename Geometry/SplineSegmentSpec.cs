using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents how a <see cref="Spline"/> gets from its previous control point to
/// its next one: a straight line if neither control point is set, a quadratic Bezier curve
/// through <see cref="Control1"/> if only it is set, or a cubic Bezier curve through both
/// <see cref="Control1"/> and <see cref="Control2"/> if both are set.  This mirrors
/// <see cref="TubeSegmentSpec"/>, minus the radius dimension.
/// </summary>
public class SplineSegmentSpec
{
    /// <summary>
    /// This property holds the curve's first control point, or <c>null</c>, for a straight
    /// line.
    /// </summary>
    public Point Control1 { get; set; }

    /// <summary>
    /// This property holds the curve's second control point, or <c>null</c>, for a straight
    /// line or a quadratic curve.
    /// </summary>
    public Point Control2 { get; set; }

    /// <summary>
    /// This property holds the point this segment ends at.
    /// </summary>
    public Point End { get; set; }
}
