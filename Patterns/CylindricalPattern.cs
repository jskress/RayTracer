using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the cylindrical pattern: a single falloff from the Y axis out to the unit
/// cylinder about it, 1 on the axis and 0 at that surface and beyond.  It is the sibling of
/// <see cref="PlanarPattern"/> and <see cref="BoxedPattern"/>, measuring the same clipped
/// distance about a different shape.
///
/// Note this is not the same thing as a "cylindrical gradient", despite the name: that one
/// repeats, being the fractional part of the distance, while this one is a single falloff that
/// stops.
/// </summary>
public class CylindricalPattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports.  In
    /// this case, the <see cref="Evaluate"/> method will return the index of the pigment
    /// to use.  If this is zero, then this pattern will return a number in the [0, 1]
    /// interval.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        // The distance from the Y axis, so Y itself plays no part.
        return InvertedClip(Math.Sqrt(point.X * point.X + point.Z * point.Z));
    }
}
