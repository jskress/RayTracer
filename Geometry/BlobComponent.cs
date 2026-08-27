using RayTracer.Basics;
using RayTracer.Pigments;

namespace RayTracer.Geometry;

/// <summary>
/// This class provides what every blob component has in common: a strength, an optional pigment of
/// its own, an optional transform of its own, and the decomposition into the low-level primitives
/// that actually contribute to the field.
/// </summary>
public abstract class BlobComponent : IBlobComponent
{
    /// <summary>
    /// This property holds the strength of the component.
    /// </summary>
    public double Strength { get; set; } = 1;

    /// <summary>
    /// This property holds the pigment this component colors the blob with, or <c>null</c>, if it
    /// leaves that to the blob's own material.
    /// </summary>
    public Pigment Pigment { get; set; }

    /// <summary>
    /// This property holds the transform this component's shape is measured in, or <c>null</c>, if
    /// it sits square in the blob's own space.
    /// </summary>
    public Matrix Transform { get; set; }

    /// <summary>
    /// This method decomposes the component into the low-level primitives that actually contribute
    /// to the blob's field, wrapping each in the component's transform when it has one.
    /// </summary>
    /// <returns>The primitives that make up this component.</returns>
    public IEnumerable<IBlobPrimitive> GetPrimitives()
    {
        IEnumerable<IBlobPrimitive> primitives = GetLocalPrimitives();

        return Transform is null
            ? primitives
            : primitives.Select(primitive => new BlobTransformedPrimitive(primitive, Transform));
    }

    /// <summary>
    /// This method must be provided by subclasses to decompose the component into primitives, in
    /// the component's own space.  Whatever transform the component carries is applied around
    /// whatever this hands back, so a subclass need never think about one.
    /// </summary>
    /// <returns>The primitives that make up this component, in its own space.</returns>
    protected abstract IEnumerable<IBlobPrimitive> GetLocalPrimitives();
}
