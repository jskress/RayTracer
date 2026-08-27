using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides a pattern of wrinkles.
/// </summary>
public class WrinklesPattern : Pattern, INoiseConsumer
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
    /// This method is the same, leaving out the layers finer than the patch a ray covers -- each
    /// faded toward what the noise comes to on average rather than toward nothing, since this
    /// noise sits about a half and not about zero.  Wrinkles sums ten layers of its own rather than
    /// coming through <see cref="LayeredNoise"/>, so the judgement is borrowed from there to keep
    /// every kind of noise fading out the same way.
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point, Footprint footprint)
    {
        double width = footprint?.Width ?? 0;
        NoiseGenerator generator = NoiseGenerator.ForSeed(Seed);
        Vector vector = new (point);
        double first = LayeredNoise.WorthSummingAt(1, width);
        double value = first <= 0
            ? NoiseGenerator.AverageValue
            : first * generator.Noise(point) + (1 - first) * NoiseGenerator.AverageValue;
        double lambda = 2;
        double omega = 0.5;

        // POV-Ray runs this from i = 1 while i < 10, so nine further octaves on top of the
        // first sample above.
        for (int i = 1; i < 10; i++)
        {
            double worth = LayeredNoise.WorthSummingAt(1 / lambda, width);

            if (worth <= 0)
            {
                value += omega * NoiseGenerator.AverageValue;
            }
            else
            {
                Vector work = vector * lambda;
                double noise = generator.Noise(new Point(work.X, work.Y, work.Z));

                value += omega * (worth * noise + (1 - worth) * NoiseGenerator.AverageValue);
            }

            // Each octave doubles the frequency: 2, 4, 8, 16...  This used to add 2 rather
            // than multiply by it, which walked the octaves up arithmetically (2, 4, 6, 8...)
            // and never reached the fine detail the halving weights below assume.
            lambda *= 2;
            omega *= 0.5;
        }

        return value / 2;
    }
}
