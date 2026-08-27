using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a sphere-shaped component of a blob.
/// </summary>
public class BlobSphereComponent : BlobComponent
{
    /// <summary>
    /// This property holds the center of the sphere.  It defaults to the origin, so that a component
    /// meant to be shaped by a transform and then moved into place need not say so twice.
    /// </summary>
    public Point Center { get; set; } = Point.Zero;

    /// <summary>
    /// This property holds the radius of the sphere.
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    /// This method decomposes the component into the low-level primitives that actually
    /// contribute to the blob's field.
    /// </summary>
    /// <returns>The primitives that make up this component.</returns>
    protected override IEnumerable<IBlobPrimitive> GetLocalPrimitives()
    {
        yield return new BlobSpherePrimitive(Center, Radius, Strength);
    }
}
