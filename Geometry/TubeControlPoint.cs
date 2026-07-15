using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents one control point along a <see cref="Tube"/>: a center point and
/// the tube's radius there.  Consecutive control points are joined by a
/// <see cref="TubeSegment"/>, with center and radius linearly interpolated between them.
/// </summary>
public class TubeControlPoint
{
    /// <summary>
    /// This property holds the location of the control point.
    /// </summary>
    public Point Center { get; set; }

    /// <summary>
    /// This property holds the tube's radius at this control point.
    /// </summary>
    public double Radius { get; set; }
}
