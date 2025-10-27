using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Geometry;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents an intersection in two dimensions. 
/// </summary>
public class TwoDIntersection
{
    /// <summary>
    /// This holds the distance from the ray to the intersection point.
    /// </summary>
    public double Distance { get; set; }

    /// <summary>
    /// This holds the point of intersection.
    /// </summary>
    public TwoDPoint Point { get; init; }

    /// <summary>
    /// This holds the 2D normal at the point of intersection.
    /// </summary>
    public TwoDVector TwoDNormal { get; init; }

    /// <summary>
    /// This method is used to convert this simple intersection into a proper one.
    /// </summary>
    /// <param name="surface">The surface that was intersected.</param>
    /// <returns>The appropriate intersection object.</returns>
    public Intersection FromXy(Surface surface)
    {
        return new PrecomputedNormalIntersection(surface, Distance, TwoDNormal.FromXy());
    }

    /// <summary>
    /// This method is used to convert this simple intersection into a proper one.
    /// </summary>
    /// <param name="surface">The surface that was intersected.</param>
    /// <returns>The appropriate intersection object.</returns>
    public Intersection FromXz(Surface surface)
    {
        return new PrecomputedNormalIntersection(surface, Distance, TwoDNormal.FromXz());
    }
}
