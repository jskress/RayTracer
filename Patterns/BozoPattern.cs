using RayTracer.Basics;

namespace RayTracer.Patterns;

/// <summary>
/// This class provides the bozo pattern: plain noise, handed straight to a colour map.  It is
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
        return NoiseGenerator.ForSeed(Seed).Noise(point);
    }
}
