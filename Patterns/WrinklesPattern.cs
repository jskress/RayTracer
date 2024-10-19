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
        Vector vector = new Vector(point);
        double value = PerlinNoise.GetNoise(Seed).Noise(point);
        double lambda = 2;
        double omega = 0.5;

        for (int _ = 0; _ < 10; _++)
        {
            Vector work = vector * lambda;

            value += omega * PerlinNoise.GetNoise(Seed).Noise(new Point(work.X, work.Y, work.Z));
            lambda += 2;
            omega *= 0.5;
        }

        return value / 2;
    }
}
