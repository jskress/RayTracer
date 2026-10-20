using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Patterns;

/// <summary>
/// This class is the pattern of how closed in a surface is: nought on an open face, rising in its corners,
/// grooves and seams, where dirt, grime and moss gather.  It looks about the place rather than at the point,
/// so it ignores turbulence and transforms; see <see cref="Weathering"/>.
/// </summary>
public class CrevicesPattern : Pattern
{
    /// <summary>
    /// This is how far a crevice is looked for when a scene says nothing.
    /// </summary>
    public const double DefaultRadius = 0.25;

    /// <summary>
    /// This property holds how far about the place to look, in the units of the space the surface stands in.
    /// </summary>
    public double Radius { get; set; } = DefaultRadius;

    /// <summary>
    /// This property notes that the pattern needs that.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This property notes that the pattern looks about the place it is asked about.
    /// </summary>
    public override bool SeesTheSurroundings => true;

    /// <summary>
    /// This method returns how closed in the place being shaded is.
    /// </summary>
    /// <param name="point">The point, which the pattern does not use.</param>
    /// <returns>How closed in the place is, from nought to one.</returns>
    public override double Evaluate(Point point) => Math.Min(Weathering.Enclosure(this, Radius), Math.BitDecrement(1.0));

    /// <summary>
    /// This method returns the same, the place being one place however much of the surface a ray covers.
    /// </summary>
    public override double Evaluate(Point point, Footprint footprint) => Evaluate(point);
}
