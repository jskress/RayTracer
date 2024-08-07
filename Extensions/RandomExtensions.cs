namespace RayTracer.Extensions;

/// <summary>
/// This class gives us some extra things on a random number generator.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// This is a helper method to give us a random double within a range.
    /// </summary>
    /// <param name="rng">The random number generator to use.</param>
    /// <param name="min">The minimum of the range.</param>
    /// <param name="max">The maximum of the range.</param>
    /// <returns>A random number in the given range.</returns>
    public static double NextDouble(this Random rng, double min, double max)
    {
        return min + rng.NextDouble() * (max - min);
    }
}
