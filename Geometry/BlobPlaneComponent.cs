using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a plane-shaped component of a blob -- a slab of field that a sphere or a
/// cylinder can settle into, which is what makes it useful for merging a blob into a floor, or for
/// giving one a flat side without cutting it.
/// </summary>
public class BlobPlaneComponent : BlobComponent
{
    /// <summary>
    /// This property holds a point the plane passes through.  It defaults to the origin.
    /// </summary>
    public Point Point { get; set; } = Point.Zero;

    /// <summary>
    /// This property holds the direction the plane faces.
    /// </summary>
    public Vector Normal { get; set; }

    /// <summary>
    /// This property holds how far the plane's influence reaches to either side of it.
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    /// This method decomposes the component into the low-level primitives that actually contribute
    /// to the blob's field.
    /// </summary>
    /// <returns>The primitives that make up this component.</returns>
    protected override IEnumerable<IBlobPrimitive> GetLocalPrimitives()
    {
        yield return new BlobPlanePrimitive(Point, Normal, Radius, Strength);
    }
}
