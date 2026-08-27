using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides a pattern of dents.
/// </summary>
public class DentsPattern : Pattern, INoiseConsumer
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
    /// Dents is a single layer of noise cubed, and the average of a cube is not the cube of an average, so once a ray covers
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
            return NoiseGenerator.AverageCubedValue;

        double noise = NoiseGenerator.ForSeed(Seed).Noise(point);

        double value = noise * noise * noise;

        return worth * value + (1 - worth) * NoiseGenerator.AverageCubedValue;
    }
}
