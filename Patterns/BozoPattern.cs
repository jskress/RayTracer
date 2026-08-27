using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the bozo pattern: plain noise, handed straight to a color map.  It is
/// the simplest of the noise patterns -- where <see cref="DentsPattern"/> cubes its noise to
/// scatter it and <see cref="GranitePattern"/> sums octaves of it, this one does nothing to it
/// at all -- which makes it the natural choice for clouds, blotches, and the gentle variation
/// that keeps a surface from looking manufactured.
/// </summary>
public class BozoPattern : Pattern, INoiseConsumer
{
    /// <summary>
    /// This property holds the seed for the noise generator to use.
    /// If it is not specified, a default noise generator will be used.
    /// </summary>
    public int? Seed { get; set; }

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
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point)
    {
        return Evaluate(point, Footprint.None);
    }

    /// <summary>
    /// This method is the same, told how much of the surface the ray covers.
    /// <para>
    /// Bozo is a single layer of noise at unit scale, so once a ray covers
    /// more than a unit there is nothing left to resolve and the honest answer is what the noise
    /// comes to on average.  It is eased into rather than switched to, so that no seam shows where
    /// the change happens.
    /// </para>
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point, Footprint footprint)
    {
        double worth = LayeredNoise.WorthSummingAt(1, footprint?.Width ?? 0);

        if (worth <= 0)
            return NoiseGenerator.AverageValue;

        double value = NoiseGenerator.ForSeed(Seed).Noise(point);

        return worth * value + (1 - worth) * NoiseGenerator.AverageValue;
    }
}
