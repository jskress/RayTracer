namespace RayTracer;

public static class DoubleExtensions
{
    public const double Epsilon = 0.00001;

    /// <summary>
    /// This method is used to convert a value in degrees to its equivalent in radians.
    /// </summary>
    /// <param name="degrees">The degrees to convert to radians.</param>
    /// <returns>The degrees as radians.</returns>
    public static double ToRadians(this double degrees)
    {
        return degrees / 180 * Math.PI;
    }

    /// <summary>
    /// This method tests two doubles to see if they are near enough to each other to be
    /// considered equivalent.
    /// </summary>
    /// <param name="left">The left number to compare.</param>
    /// <param name="right">The right number to compare.</param>
    /// <returns><c>true</c>, if the two numbers are close enough, or <c>false</c>
    /// if not.</returns>
    public static bool Near(this double left, double right)
    {
        return Math.Abs(left - right) < Epsilon;
    }
}
