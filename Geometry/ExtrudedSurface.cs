namespace RayTracer.Geometry;

/// <summary>
/// This class provides the common base class for surfaces that are extruded, like cylinders,
/// cones and generalized extrusions.
/// </summary>
public abstract class ExtrudedSurface : Surface
{
    /// <summary>
    /// This property holds the minimum value of Y for the extent of the extrusion.  This
    /// is in object space.
    /// </summary>
    public double MinimumY { get; set; } = -1;

    /// <summary>
    /// This property holds the maximum value of Y for the extent of the extrusion.  This
    ///  is in object space.
    /// </summary>
    public double MaximumY { get; set; } = 1;

    /// <summary>
    /// This property notes whether the extruded surface has end caps. It is relevant only
    /// when either or both <c>MinimumY</c> and <c>MaximumY</c> are not infinite.
    /// </summary>
    public bool Closed { get; set; } = true;
}
