using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class represents a simple intersection that carries a ray distance to the
/// intersection and the normal at the intersection point.
/// This is typically used as a transient thing and provides the means to be promoted to
/// a true intersection.
/// </summary>
public class SimpleIntersection
{
    private readonly double _distance;
    private readonly Vector _normal;

    public SimpleIntersection(double distance, Vector normal)
    {
        _distance = distance;
        _normal = normal;
    }

    /// <summary>
    /// This method is used to convert this simple intersection into a proper one.
    /// </summary>
    /// <param name="surface">The surface that was intersected.</param>
    /// <returns>The appropriate intersection object.</returns>
    public Intersection AsIntersection(Surface surface)
    {
        return new PrecomputedNormalIntersection(surface, _distance, _normal);
    }
}
