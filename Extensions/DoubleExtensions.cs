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
    /// This is the tolerance to use when a quantity must be judged against the sizes that
    /// produced it rather than against a fixed number.  <see cref="Epsilon"/> is right for a
    /// length in world units at human scale, and wrong for anything that grows or shrinks with
    /// a surface's transform: carrying a world ray into the space of a surface scaled by 100
    /// divides the ray's direction by 100, so every quantity derived from that direction falls
    /// with it.  Comparing such a quantity against a fixed number silently loses geometry as a
    /// scene is scaled up.
    /// </summary>
    public const double RelativeTolerance = 0.000001;

    /// <summary>
    /// This method reports whether a value is too small to be told apart from nothing when set
    /// beside the scale of the quantities that produced it.  Both must carry the same units for
    /// the comparison to mean anything.
    /// </summary>
    /// <param name="value">The value to judge.</param>
    /// <param name="scale">The size of the quantities the value came from.</param>
    /// <returns><c>true</c>, if the value is negligible beside that scale.</returns>
    public static bool IsNegligibleBeside(this double value, double scale)
    {
        return Math.Abs(value) <= Math.Abs(scale) * RelativeTolerance;
    }

    /// <summary>
    /// This method is the same test as <see cref="IsNegligibleBeside"/> for a pair of values
    /// that are already squared, which is how the ray/surface tests carry them so as to avoid a
    /// square root in the intersection path.  Squaring both sides squares the tolerance too.
    /// </summary>
    /// <param name="squaredValue">The squared value to judge.</param>
    /// <param name="squaredScale">The squared size of the quantities the value came from.</param>
    /// <returns><c>true</c>, if the value is negligible beside that scale.</returns>
    public static bool IsNegligibleSquaredBeside(this double squaredValue, double squaredScale)
    {
        return Math.Abs(squaredValue) <= Math.Abs(squaredScale) * (RelativeTolerance * RelativeTolerance);
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
