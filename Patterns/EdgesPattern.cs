using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Patterns;

/// <summary>
/// This class is the pattern of how far a surface stands out: nought on a flat face of a solid, rising toward
/// its corners, rims and ridges, where paint wears through and handling polishes.  It looks into the solid
/// the surface is the skin of rather than at the point, so it ignores turbulence and transforms; a sheet's
/// far side is not an edge.  See <see cref="Weathering"/>.
/// </summary>
public class EdgesPattern : Pattern
{
    /// <summary>
    /// This is how far an edge is looked for when a scene says nothing.
    /// </summary>
    public const double DefaultRadius = 0.25;

    /// <summary>
    /// This property holds how far into the solid to look, in the units of the space the surface stands in.
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
    /// This method returns how far the place being shaded stands out.
    /// </summary>
    /// <param name="point">The point, which the pattern does not use.</param>
    /// <returns>How far the place stands out, from nought to one.</returns>
    public override double Evaluate(Point point) => Math.Min(Weathering.Exposure(this, Radius), Math.BitDecrement(1.0));

    /// <summary>
    /// This method returns the same, the place being one place however much of the surface a ray covers.
    /// </summary>
    public override double Evaluate(Point point, Footprint footprint) => Evaluate(point);
}
