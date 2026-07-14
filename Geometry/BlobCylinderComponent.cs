using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a cylinder-shaped component of a blob.  It decomposes into a
/// capped cylindrical body plus two hemispherical caps, so the resulting shape is a smooth
/// capsule rather than a cylinder with sharp-edged ends.
/// </summary>
public class BlobCylinderComponent : IBlobComponent
{
    /// <summary>
    /// This property holds the center of the cylinder's base.
    /// </summary>
    public Point Start { get; set; }

    /// <summary>
    /// This property holds the center of the cylinder's apex.
    /// </summary>
    public Point End { get; set; }

    /// <summary>
    /// This property holds the radius of the cylinder.
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
        Vector axisUnit = (End - Start).Unit;

        yield return new BlobCylinderBodyPrimitive(Start, End, Radius, Strength);
        yield return new BlobSpherePrimitive(Start, Radius, Strength, axisUnit);
        yield return new BlobSpherePrimitive(End, Radius, Strength, -axisUnit);
    }
}
