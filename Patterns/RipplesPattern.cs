using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the ripples pattern: concentric rings spreading from a handful of scattered
/// sources, averaged together so that they interfere the way ripples on water do.
/// <para>
/// It exists chiefly to roughen a surface rather than to colour one.  Every source rings at the
/// same rate, which is what sets it apart from <see cref="WavesPattern"/>, and what makes it read
/// as a still surface disturbed at a few points rather than as open water.
/// </para>
/// </summary>
public class RipplesPattern : Pattern
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
    /// The frequency and phase are read here as well as where every pattern's value is shaped,
    /// which looks like saying them twice and is POV-Ray's arrangement rather than an oversight.
    /// Here they set how tightly the rings are spaced; there they scale the number that comes out.
    /// Left alone, as they nearly always are, the second reading changes nothing.
    /// </para>
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        return 0.5 * (1 + WaveSources.Sum(point, Frequency, Phase, perSourceFrequency: false));
    }
}
