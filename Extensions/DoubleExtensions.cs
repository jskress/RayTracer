namespace RayTracer.Extensions;

public static class DoubleExtensions
{
    public const double Epsilon = 0.000001;

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
    /// This method is used to convert a value in radians to its equivalent in degrees.
    /// </summary>
    /// <param name="degrees">The radians to convert to degrees.</param>
    /// <returns>The radians as degrees.</returns>
    public static double ToDegrees(this double degrees)
    {
        return degrees * 180 / Math.PI;
    }

    /// <summary>
    /// This method tests two doubles to see if they are near enough to each other to be
    /// considered equivalent.
    /// </summary>
    /// <param name="left">The left number to compare.</param>
    /// <param name="right">The right number to compare.</param>
    /// <param name="epsilon">The error range to use.</param>
    /// <returns><c>true</c>, if the two numbers are close enough, or <c>false</c>
    /// if not.</returns>
    public static bool Near(this double left, double right, double epsilon = Epsilon)
    {
        return Math.Abs(left - right) < epsilon;
    }

    /// <summary>
    /// This method is used to return the fractional part of a number.
    /// </summary>
    /// <param name="number">The number to get the fractional part of.</param>
    /// <returns>The fractional part of the number.</returns>
    public static double Fraction(this double number)
    {
        return number - Math.Floor(number);
    }
}
