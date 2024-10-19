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
        Vector vector1 = new Vector(point) * 4;
        double noise = 0;
        double frequency = 1;

        for (int count = 0; count < 6; count++)
        {
            Vector vector2 = vector1 * frequency;
            double number = Math.Abs(0.5 - PerlinNoise.GetNoise(Seed).Noise(
                new Point(vector2.X, vector2.Y, vector2.Z)));

            noise += number / frequency;
            frequency *= 2;
        }

        return noise;
    }
}
