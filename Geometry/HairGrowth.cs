using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class describes hair to be grown over a surface: how much of it, and the shape of each strand.
/// The surface it is written in grows it, as <see cref="Hair"/>, when it is readied for rendering.
/// <para>
/// Lengths and widths are in the space the surface is placed in -- its parent's -- rather than in the
/// surface's own, so a cube scaled flat into a rug grows a pile of the length asked for rather than one
/// squashed by the scaling.
/// </para>
/// </summary>
public class HairGrowth
{
    /// <summary>
    /// This property holds how many strands to grow on each unit of the surface's area.
    /// </summary>
    public double? Density { get; set; }

    /// <summary>
    /// This property holds exactly how many strands to grow, whatever the surface's area.
    /// </summary>
    public int? Count { get; set; }

    /// <summary>
    /// This property holds how long a strand is.
    /// </summary>
    public double Length { get; set; }

    /// <summary>
    /// This property holds how much one strand's length may differ from another's, as a fraction of it
    /// either way.
    /// </summary>
    public double Vary { get; set; } = 0.2;

    /// <summary>
    /// This property holds how wide a strand is at its root, from which it tapers to a point.  Unsaid, it
    /// is a fiftieth of the length.
    /// </summary>
    public double? Thickness { get; set; }

    /// <summary>
    /// This property holds how far a strand leans away from straight out of the surface, in radians: from
    /// nought, standing straight out, to a right angle, lying along it.
    /// </summary>
    public double Lean { get; set; }

    /// <summary>
    /// This property holds the direction strands lean toward, as fur lies along a body; unsaid, each
    /// leans its own way.
    /// </summary>
    public Vector Comb { get; set; }

    /// <summary>
    /// This property holds how many times a strand winds about its own line from root to tip.
    /// </summary>
    public double Curl { get; set; }

    /// <summary>
    /// This property holds how far from its line a curling strand winds.  Unsaid, a twelfth of its length.
    /// </summary>
    public double? CurlRadius { get; set; }

    /// <summary>
    /// This property holds how far a strand wanders at random, as a fraction of its length.
    /// </summary>
    public double Frizz { get; set; }

    /// <summary>
    /// This property holds how strongly neighboring strands gather into locks, from nought to one.
    /// </summary>
    public double Clump { get; set; }

    /// <summary>
    /// This property holds how many strands make a lock.
    /// </summary>
    public int ClumpSize { get; set; } = 30;

    /// <summary>
    /// This property holds how far a strand sags under its own weight: nought stands straight, and one
    /// bends a strand growing sideways until its tip hangs down.
    /// </summary>
    public double Droop { get; set; }

    /// <summary>
    /// This property holds the seed the hair's randomness starts from.
    /// </summary>
    public int? Seed { get; set; }

    /// <summary>
    /// This property holds the hair's material.  Unsaid, each strand wears the material of the part of the
    /// surface it grows from.
    /// </summary>
    public Material Material { get; set; }
}
