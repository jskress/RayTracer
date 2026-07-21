using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Pigments;

/// <summary>
/// This class dims a wrapped pigment by summed noise, leaving it mottled -- light in some places
/// and dark in others.
/// <para>
/// It is worth being clear how this differs from the turbulence a pattern may carry, since the two
/// are easily confused and the old name for this ("noisy") invited exactly that.  Turbulence stirs
/// the *point* a pattern is asked about, so a straight boundary comes out ragged; this multiplies
/// the *colour* that came back, so a flat pigment comes out blotchy.  Neither can stand in for the
/// other, and this one is the only way to make a plain, patternless colour vary at all.
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
        Pigment.SetSeed(seed);
        Noise.Seed ??= seed;
    }

    /// <summary>
    /// This method accepts a point and produces a color for that point.  We return the colour our
    /// wrapped pigment gives, dimmed by the noise at that point.
    /// </summary>
    /// <param name="point">The point to produce a color for.</param>
    /// <returns>The appropriate color at the given point.</returns>
    /// <summary>
    /// Dimming a colour cannot make it let light through, so this is wholly the wrapped pigment's answer.
    /// </summary>
    public override bool MayTransmit => Pigment.MayTransmit;

    public override Color GetColorFor(Point point)
    {
        return Pigment.GetColorFor(point) * Noise.Generate(point);
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
