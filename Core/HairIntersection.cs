using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Core;

/// <summary>
/// This class represents a ray crossing a strand of hair: where along the strand it crossed, where
/// across it, and the normal it is shaded with there.
/// </summary>
public class HairIntersection : Intersection
{
    /// <summary>
    /// This property holds the normal the strand is shaded with at the crossing, in the strands' own
    /// space: the way back along the ray, turned toward the strand's nearer edge as a cylinder's would be.
    /// </summary>
    public Vector StrandNormal { get; }

    /// <summary>
    /// This property holds how far along its strand the crossing is: nought at the root, one at the tip.
    /// </summary>
    public double Along { get; }

    /// <summary>
    /// This property holds where across the strand the ray passed, from minus one at one edge through
    /// nought at its middle to one at the other.
    /// </summary>
    public double Across { get; }

    /// <summary>
    /// This property holds which strand of its set the crossing is on.
    /// </summary>
    public int Strand { get; }

    /// <summary>
    /// This property holds the point on the strand's middle line nearest the crossing, in the strands'
    /// own space.
    /// </summary>
    public Point Center { get; }

    /// <summary>
    /// This property holds half the strand's width at the crossing, in the strands' own space.
    /// </summary>
    public double Radius { get; }

    /// <summary>
    /// This property holds how much of the ray the strand covers where it crossed it, from nought to one:
    /// one, unless the strand is finer than the ray is wide there, when it is widened to the ray's width
    /// and covers only its own share of it.  See <see cref="StrandSet"/>.
    /// </summary>
    public double Coverage { get; init; } = 1;

    /// <summary>
    /// This property holds how much of what the eye sees along its ray the strand's shading is counted
    /// for: its coverage, times what the strands before it left uncovered.  The strands deep in a fine
    /// head of hair are each counted for very little, and are shaded with fewer of each light's samples
    /// accordingly; see <see cref="Scene"/>'s shading of a fiber.
    /// </summary>
    public double Share { get; set; } = 1;

    /// <summary>
    /// This property holds how many strands the eye's ray met before this one, which sets where among a
    /// light's samples a strand shaded with only a few of them starts, so that the strands of one ray
    /// look at different parts of the light rather than all at the same one.
    /// </summary>
    public int Ordinal { get; set; }

    /// <summary>
    /// This property holds the way the strand runs at the crossing, root to tip, as a unit vector in the
    /// strands' own space.
    /// </summary>
    public Vector Tangent { get; }

    /// <summary>
    /// This property reports whether the strand is a wire rather than a fiber: one whose own material is a
    /// metal, as a guitar's strings or a piano's are.  A wire is shaded as the round surface it is rather
    /// than as a fiber, and stops the light falling on it; see <see cref="Scene"/>.  A strand wearing the
    /// material of what it grows from is hair, whatever that is.
    /// </summary>
    public bool IsAWire =>
        Surface is StrandSurface { WearsItsSkin: false } strands && (strands.Material?.Metallic ?? 0) > 0;

    /// <summary>
    /// This property reports whether the strand is a wire finer than the ray is wide, which the ray sees the
    /// whole round of at once, and which is shaded round its whole round; see <see cref="Scene"/>.
    /// </summary>
    public bool IsAFineWire => Coverage < 1 && IsAWire;

    /// <summary>
    /// This property holds the sine of the angle between the ray and the plane square to the strand,
    /// which is how steeply the ray crossed it.  It is what a shadow ray needs to know of a strand it
    /// crosses, and has it without working out anything in the world.
    /// </summary>
    public double SineAlong { get; }

    public HairIntersection(
        Surface surface, double distance, Vector normal, double along, double across, int strand = 0,
        Point center = null, double radius = 0, Vector tangent = null, double sineAlong = 0)
        : base(surface, distance)
    {
        Tangent = tangent;
        SineAlong = sineAlong;
        StrandNormal = normal;
        Along = along;
        Across = across;
        Strand = strand;
        Center = center;
        Radius = radius;
    }

    /// <summary>
    /// This method returns the points a ray leaving this crossing sets off from: the strand's own
    /// surface, as though it were round, on the side the crossing faces and on the far side.
    /// <para>
    /// **They cannot be the crossing itself, nudged.**  The crossing is on a flat ribbon through the
    /// strand's middle, so it lies inside the round strand the shading pretends it is -- up to half
    /// the width away from where that strand's surface would be.  A ray toward a light, set off from
    /// there, meets its own strand again: the ribbon it is tested against faces *it*, and lies across
    /// its path whenever the crossing was on the side away from the light.  Half of every strand went
    /// dark that way, the half a round strand would have had lit.  Set off from the round surface
    /// instead, a ray that leaves outward -- every ray a lit point sends to a light, and every
    /// reflection -- is moving away from the strand's middle from its first step, and never meets it.
    /// </para>
    /// </summary>
    /// <param name="adjustment">The usual nudge, along the normal, in the world.</param>
    /// <returns>The points to set off from, on this side and the far side.</returns>
    protected override (Point Over, Point Under) LeavingPoints(Vector adjustment)
    {
        if (Center is null)
            return base.LeavingPoints(adjustment);

        Point near = Surface.SurfaceToWorld(Center + StrandNormal * Radius, TimeIndex, Portals);
        Point far = Surface.SurfaceToWorld(Center - StrandNormal * Radius, TimeIndex, Portals);

        return (near + adjustment, far - adjustment);
    }
}
