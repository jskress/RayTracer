using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class dims a wrapped pigment by summed noise, leaving it mottled -- light in some places
/// and dark in others.
/// <para>
/// It is worth being clear how this differs from the turbulence a pattern may carry, since the two
/// are easily confused and the old name for this ("noisy") invited exactly that.  Turbulence stirs
/// the *point* a pattern is asked about, so a straight boundary comes out ragged; this multiplies
/// the *color* that came back, so a flat pigment comes out blotchy.  Neither can stand in for the
/// other, and this one is the only way to make a plain, patternless color vary at all.
/// </para>
/// </summary>
public class MottledPigment : Pigment
{
    /// <summary>
    /// This property holds the pigment to apply noise to.
    /// </summary>
    public Pigment Pigment { get; init; }

    /// <summary>
    /// This property holds the noise to dim the wrapped pigment by.  It is plain layered noise
    /// rather than turbulence, because there is nothing here to push: an amplitude says how far to
    /// move a point, and this moves no points.
    /// </summary>
    public LayeredNoise Noise { get; init; }

    /// <summary>
    /// This method is used to push any random number generator seeds throughout the pigment
    /// tree.
    /// </summary>
    /// <param name="seed">The seed value to set.</param>
    public override void SetSeed(int seed)
    {
        if (!Pigment.Seed.HasValue)
            Pigment.SetSeed(seed);

        Noise.Seed ??= seed;
    }

    /// <summary>
    /// This method passes the chance to get ready along to the pigment we dim.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="surface">The surface that this pigment is set on.</param>
    protected override void PrepareForRendering(RenderContext context, Surface surface)
    {
        Pigment.RenderingIsAboutToStart(context, surface);
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  We return the color our
    /// wrapped pigment gives, dimmed by the noise at that point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    /// <summary>
    /// Dimming a color cannot make it let light through, so this is wholly the wrapped pigment's answer.
    /// </summary>
    public override bool MayTransmit => Pigment.MayTransmit;

    public override Color GetColorFor(Point point)
    {
        return Pigment.GetColorFor(point) * Noise.Generate(point);
    }

    /// <summary>
    /// This method is the same, told how much of the surface the ray covers, so that mottling too
    /// fine to be seen is faded out rather than left to shimmer.
    /// <para>
    /// Both halves get the patch: the wrapped pigment, which may be a pattern with detail of its
    /// own, and the noise doing the dimming, whose finer layers are dropped once they are smaller
    /// than the patch.
    /// </para>
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <param name="footprint">The patch around it, in this pigment's own space.</param>
    /// <returns>The appropriate color.</returns>
    public override Color GetColorFor(Point point, Footprint footprint)
    {
        return Pigment.GetColorFor(point, footprint) * Noise.Generate(point, footprint?.Width ?? 0);
    }

    /// <summary>
    /// This method returns whether the given pigmentation matches this one.
    /// </summary>
    /// <param name="other">The pigmentation to compare to.</param>
    /// <returns><c>true</c>, if the two pigmentations match, or <c>false</c>, if not.</returns>
    public override bool Matches(Pigment other)
    {
        return other is MottledPigment pigmentation &&
               Pigment.Matches(pigmentation.Pigment);
    }
}
