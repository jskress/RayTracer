namespace RayTracer.Geometry;

/// <summary>
/// This interface represents one user-configurable component (sphere or cylinder) that
/// contributes to a blob's field.  A component may decompose into more than one low-level
/// primitive; a cylinder, for instance, is a capped cylindrical body plus two hemispherical
/// caps, so that the overall shape has no sharp edges where it merges with other components.
/// </summary>
public interface IBlobComponent
{
    /// <summary>
    /// This property holds the strength of the component.  Positive values add to the
    /// field (the usual case); negative values subtract from it, letting a component carve
    /// into neighboring components instead of adding to them.
    /// </summary>
    double Strength { get; set; }

    /// <summary>
    /// This method decomposes the component into the low-level primitives that actually
    /// contribute to the blob's field.
    /// </summary>
    /// <returns>The primitives that make up this component.</returns>
    IEnumerable<IBlobPrimitive> GetPrimitives();
}
