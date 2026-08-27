using RayTracer.Basics;
using RayTracer.Pigments;

namespace RayTracer.Geometry;

/// <summary>
/// This interface represents one user-configurable component (a sphere, a cylinder or a plane)
/// that contributes to a blob's field.  A component may decompose into more than one low-level
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
    /// This property holds the pigment this component colors the blob with, or <c>null</c>, if it
    /// leaves that to the blob's own material.  Where two components that carry pigments overlap,
    /// their colors mix in the proportion each contributes to the field there, so the color changes
    /// over exactly where the shape does.
    /// </summary>
    Pigment Pigment { get; set; }

    /// <summary>
    /// This property holds the transform the component's shape is measured in, or <c>null</c>, if
    /// it sits square in the blob's own space.  It is what turns a sphere into an ellipsoid and a
    /// cylinder into an elliptical bond.
    /// </summary>
    Matrix Transform { get; set; }

    /// <summary>
    /// This method decomposes the component into the low-level primitives that actually
    /// contribute to the blob's field.
    /// </summary>
    /// <returns>The primitives that make up this component.</returns>
    IEnumerable<IBlobPrimitive> GetPrimitives();
}
