using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents one point along a <see cref="Ribbon"/>: where it passes, and how wide it is
/// there.
/// </summary>
public class RibbonControlPoint
{
    /// <summary>
    /// This property holds the point the ribbon passes through.
    /// </summary>
    public Point Position { get; set; }

    /// <summary>
    /// This property holds how wide the ribbon is here, measured across the whole strip rather than
    /// out from its middle.
    /// </summary>
    public double Width { get; set; }
}
