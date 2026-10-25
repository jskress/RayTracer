using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the ripples pattern: concentric rings spreading from a handful of scattered
/// sources, averaged together so that they interfere the way ripples on water do.
/// <para>
/// It exists chiefly to roughen a surface rather than to color one.  Every source rings at the
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

    // The frequency and phase set how tightly the rings are spaced, and are not read a second time on the value: there
    // they cut each smooth ring into a sawtooth, which a `normal` turned into creases.
    protected override bool SpacesItselfByFrequency => true;

    /// <summary>
    /// This method is used to determine an appropriate value, typically between 0 and 1,
    /// for the given point.
    /// <para>
    /// The frequency and phase are read here, as how tightly the rings are spaced, and only here: read again where
    /// every other pattern's value is shaped, a frequency of 7 cut each ring into seven teeth.
    /// </para>
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        return 0.5 * (1 + WaveSources.Sum(point, Frequency, Phase, perSourceFrequency: false));
    }
}
