namespace RayTracer.Geometry;

/// <summary>
/// This class provides the common base class for cylinders and cones.
/// </summary>
public abstract class CircularSurface : Surface
{
    /// <summary>
    /// This property holds the minimum value of Y for the extent of the conic.  This
    /// is in object space.
    /// </summary>
    public double MinimumY { get; set; } = -1;

    /// <summary>
    /// This property holds the maximum value of Y for the extent of the conic.  This
    ///  is in object space.
    /// </summary>
    public double MaximumY { get; set; } = 1;

    /// <summary>
    /// This property notes whether the circular surface has end caps. It is relevant only
    /// when  either or both <c>MinimumY</c> and <c>MaximumY</c> are not infinite.
    /// </summary>
    public bool Closed { get; set; } = true;
}
