using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the waves pattern: the same scattered sources as
/// <see cref="RipplesPattern"/>, but each ringing at its own rate.
/// <para>
/// That one difference is what separates a pond from the open sea.  Where every source rings alike
/// the crests line up and the surface reads as regular; give each its own wavelength and they never
/// quite agree, so long swells and short chop sit on top of one another.  The slower a source
/// rings, the further its crests carry, which is why each source's contribution is divided by its
/// own rate rather than counted equally.
/// </para>
/// </summary>
public class WavesPattern : Pattern
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
    /// <para>
    /// The five and the two and a half are POV-Ray's, and they are there because the sum being
    /// brought into range is not the tidy one the ripples pattern has: dividing each source's sine
    /// by its own rate lets the slow ones count for much more than the fast, and these are the
    /// numbers that put the result back into the neighbourhood of zero and one.  They do not
    /// promise it: a point where the slow sources happen to agree can still overshoot, and the
    /// colour map wraps it when it does.
    /// </para>
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        return 0.2 * (2.5 + WaveSources.Sum(point, Frequency, Phase, perSourceFrequency: true));
    }
}
