using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a sphere-shaped component of a blob.
/// </summary>
public class BlobSphereComponent : IBlobComponent
{
    /// <summary>
    /// This property holds the center of the sphere.
    /// </summary>
    public Point Center { get; set; }

    /// <summary>
    /// This property holds the radius of the sphere.
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    /// This property holds the strength of the component.
    /// </summary>
    public double Strength { get; set; } = 1;

    /// <summary>
    /// This method decomposes the component into the low-level primitives that actually
    /// contribute to the blob's field.
    /// </summary>
    /// <returns>The primitives that make up this component.</returns>
    public IEnumerable<IBlobPrimitive> GetPrimitives()
    {
        yield return new BlobSpherePrimitive(Center, Radius, Strength);
    }
}
