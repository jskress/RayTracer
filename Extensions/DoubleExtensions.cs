namespace RayTracer.Extensions;

/// <summary>
/// These are some helpful extensions for doubles.
/// </summary>
public static class DoubleExtensions
{
    private static readonly Random Rng = new ();

    /// <summary>
    /// This method returns a random double between 0 and 1.
    /// </summary>
    /// <returns>A random double value.</returns>
    public static double RandomDouble()
    {
        return Rng.NextDouble();
    }

    /// <summary>
    /// This method returns a random double between the given minimum and maximum.
    /// </summary>
    /// <param name="min">The low end of the random double range.</param>
    /// <param name="max">The high end of the random double range.</param>
    /// <returns>A random double between <c>min</c> and <c>max</c>.</returns>
    public static double RandomDouble(double min, double max)
    {
        return min + RandomDouble() * (max - min);
    }

    /// <summary>
    /// This method assumes the double value represents an angle in degrees and returns
    /// its value in radians.
    /// </summary>
    /// <param name="degrees">The angle in degrees to convert to radians.</param>
    /// <returns>The angle in radians.</returns>
    public static double ToRadians(this double degrees)
    {
        return Math.PI * degrees / 180;
    }
}
