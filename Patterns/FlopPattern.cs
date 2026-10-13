using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the flop pattern, whose value is not a matter of where on a surface a point is
/// but of the angle it is seen at: the angle between the way to the eye and the way the light mirrors
/// off the surface there, from nought looking straight along the reflection to one looking straight
/// back toward the light.
/// <para>
/// **It is how a color-shifting paint is made.**  Some car paints are deep blue seen from one side and
/// dark green from the other, through teal between, and a body's curves sweep a panel through the whole
/// of it at once.  The paint's color depends on the angles between the eye, the surface and the light --
/// so it is worked out once for every light, and from either oblique side of a panel the two ends of
/// the color map are seen, since one side looks toward the light's reflection and the other away.
/// Painters call the color seen away from square-on the "flop", whence the name.
/// </para>
/// <para>
/// Where no particular light is being shaded for -- what a surface lets through, the tint it gives a
/// reflection -- the light is taken to be at the eye.  Asked about a point and nothing else, as by
/// something that knows no directions at all, it answers nought.
/// </para>
/// </summary>
public class FlopPattern : Pattern
{
    /// <summary>
    /// This property reports the number of discrete pigments this pattern supports, which is none: its
    /// value runs smoothly from nought to one.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This method answers for a point alone, with no directions to go on: nought, the color seen along
    /// the reflection.
    /// </summary>
    /// <param name="point">The point, which does not matter.</param>
    /// <returns>Nought.</returns>
    public override double Evaluate(Point point)
    {
        return 0;
    }

    /// <summary>
    /// This method answers with the angle between the way to the eye and the way the light mirrors off the
    /// surface, as a share of a half turn.
    /// </summary>
    /// <param name="point">The point, which does not matter.</param>
    /// <param name="footprint">How much of the surface the ray covers, which does not matter either.</param>
    /// <param name="viewing">The directions to the eye and the light, and the way the surface faces.</param>
    /// <returns>The angle, from nought to one.</returns>
    public override double Evaluate(Point point, Footprint footprint, Viewing viewing)
    {
        // Short of one by the least amount there is: a color map wraps one back round to nought, which is
        // right for a pattern that repeats and wrong for an angle, whose two ends are two different colors.
        return Math.Min(viewing.Aspecular / Math.PI, Math.BitDecrement(1.0));
    }
}
