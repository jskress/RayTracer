using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Pigments;

namespace RayTracer.Patterns;

/// <summary>
/// This class is the pattern of what weather did to a height field's ground: one of the maps its erosion keeps --
/// where water runs, where grit was laid down, where ground was carried away (<see cref="ErosionMap"/>) -- read at
/// the place in the field's own space.  It is read on the field it is worn by, found up from the part of it being
/// shaded; worn by anything else it is nought.
/// </summary>
public class ErosionPattern : Pattern
{
    /// <summary>
    /// This property holds which of the field's maps the pattern reads.
    /// </summary>
    public ErosionMap Map { get; set; }

    /// <summary>
    /// This property notes that the pattern needs no discrete pigments.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This method returns what the map holds at the given point.
    /// </summary>
    /// <param name="point">The point, in the field's own space.</param>
    /// <returns>What the map holds there, from nought to under one.</returns>
    public override double Evaluate(Point point)
    {
        for (Surface surface = Pigment.Wearer; surface is not null; surface = surface.Parent)
        {
            if (surface is HeightField field)
                return Math.Min(field.MapAt(Map, point), Math.BitDecrement(1.0));
        }

        return 0;
    }
}
