using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the granite pattern.
/// </summary>
public class GranitePattern : Pattern, INoiseConsumer
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
    /// This method is the same, leaving out the layers finer than the patch a ray covers.
    /// <para>
    /// Granite sums its own layers rather than going through <see cref="LayeredNoise"/>, so the
    /// judgement about which are worth summing has to be made here -- but it is the same judgement,
    /// borrowed from there, so that every kind of noise fades out the same way.  Its grain is
    /// sampled at four times the point and doubles five times over, so the finest layer draws things
    /// about a hundred and twenty-eighth of a unit across: far below what a pixel holds at any
    /// distance, and the first thing to start shimmering.
    /// </para>
    /// </summary>
    /// <param name="point">The point from which the pattern value is to be derived.</param>
    /// <param name="footprint">How much of the surface the ray covers there.</param>
    /// <returns>The derived pattern value.</returns>
    public override double Evaluate(Point point, Footprint footprint)
    {
        double width = footprint?.Width ?? 0;
        Vector vector1 = new Vector(point) * 4;
        double noise = 0;
        double frequency = 1;

        for (int count = 0; count < 6; count++)
        {
            double worth = LayeredNoise.WorthSummingAt(1 / (4 * frequency), width);
            double number;

            if (worth <= 0)
            {
                // Still worth what it is worth on average.  Dropping a layer outright instead took
                // this pattern *further* from a supersampled truth than no filtering at all -- 6.28
                // against 3.11 -- because what is rectified about its middle does not average to
                // nothing.  See NoiseGenerator.AverageRectifiedValue.
                number = NoiseGenerator.AverageRectifiedValue;
            }
            else
            {
                Vector vector2 = vector1 * frequency;

                number = Math.Abs(0.5 - NoiseGenerator.ForSeed(Seed).Noise(
                    new Point(vector2.X, vector2.Y, vector2.Z)));
                number = worth * number + (1 - worth) * NoiseGenerator.AverageRectifiedValue;
            }

            noise += number / frequency;
            frequency *= 2;
        }

        return noise;
    }
}
