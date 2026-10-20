using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Patterns;

/// <summary>
/// This class is the pattern of which way a surface faces in the world: one where it faces the pattern's
/// direction, a half where it stands square to it, nought where it faces away.  Up, unless it says
/// otherwise, which is where dust, sand and snow settle; a direction away from the sun is where moss grows.
/// It is read in the world rather than in the surface's own space, since what falls, falls the world's way;
/// so it ignores turbulence and transforms.
/// </summary>
public class FacingPattern : Pattern
{
    /// <summary>
    /// This property holds the direction, in the world, that a surface facing gives the pattern its most.
    /// </summary>
    public Vector Toward { get; set; } = new (0, 1, 0);

    /// <summary>
    /// This property notes that the pattern needs that.
    /// </summary>
    public override int DiscretePigmentsNeeded => 0;

    /// <summary>
    /// This property notes that the pattern looks at the place it is asked about, rather than at the point.
    /// </summary>
    public override bool SeesTheSurroundings => true;

    /// <summary>
    /// This method returns which way the place being shaded faces.
    /// </summary>
    /// <param name="point">The point, which the pattern does not use.</param>
    /// <returns>How far it faces the pattern's way, from nought to one.</returns>
    public override double Evaluate(Point point) => Math.Min(Weathering.Facing(Toward), Math.BitDecrement(1.0));

    /// <summary>
    /// This method returns the same, the place being one place however much of the surface a ray covers.
    /// </summary>
    public override double Evaluate(Point point, Footprint footprint) => Evaluate(point);
}
