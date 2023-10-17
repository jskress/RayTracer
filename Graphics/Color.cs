using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a color in the raytracer.
/// </summary>
public class Color
{
    public static readonly Color Black = new ();
    public static readonly Color Gray = new (0.3, 0.3, 0.3);
    public static readonly Color White = new (1, 1, 1);

    /// <summary>
    /// This method generates a random color.
    /// </summary>
    /// <param name="min">The minimum value for the returned random number; defaults to
    /// zero.</param>
    /// <param name="max">The maximum value for the returned random number; defaults to
    /// one.</param>
    /// <returns>The random color.</returns>
    public static Color Random(double min = 0, double max = 1)
    {
        double red = DoubleExtensions.RandomDouble(min, max);
        double green = DoubleExtensions.RandomDouble(min, max);
        double blue = DoubleExtensions.RandomDouble(min, max);

        return new Color(red, green, blue);
    }

    /// <summary>
    /// This property returns the red component of the color.
    /// </summary>
    public double Red { get; }

    /// <summary>
    /// This property returns the green component of the color.
    /// </summary>
    public double Green { get; }

    /// <summary>
    /// This property returns the blue component of the color.
    /// </summary>
    public double Blue { get; }

    /// <summary>
    /// This property returns the alpha component of the color.
    /// </summary>
    public double Alpha { get; }

    public Color() : this(0, 0, 0) {}

    public Color(Vector vector) : this(vector.X, vector.Y, vector.Z) {}

    public Color(double red, double green, double blue, double alpha = 0)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    // -------------------------------------------------------------------------
    // Operators
    // -------------------------------------------------------------------------

    /// <summary>
    /// Add two colors.
    /// </summary>
    /// <param name="left">The left color to add.</param>
    /// <param name="right">The right color to add.</param>
    /// <returns>The sum of the colors.</returns>
    public static Color operator +(Color left, Color right)
    {
        return new Color(
            left.Red + right.Red,
            left.Green + right.Green,
            left.Blue + right.Blue,
            left.Alpha + right.Alpha);
    }

    /// <summary>
    /// Add a number to a color.
    /// </summary>
    /// <param name="left">The color to add the number to.</param>
    /// <param name="right">The number to add.</param>
    /// <returns>The sum of the color and the number.</returns>
    public static Color operator +(Color left, double right)
    {
        return new Color(
            left.Red + right,
            left.Green + right,
            left.Blue + right,
            left.Alpha);
    }

    /// <summary>
    /// Multiply a color by a number.
    /// </summary>
    /// <param name="left">The color to multiply.</param>
    /// <param name="right">The number to multiply.</param>
    /// <returns>The result of multiplying the color by the number.</returns>
    public static Color operator *(Color left, double right)
    {
        return new Color(
            left.Red * right,
            left.Green * right,
            left.Blue * right,
            left.Alpha * right);
    }

    /// <summary>
    /// Multiply two colors together.
    /// </summary>
    /// <param name="left">The first color to multiply.</param>
    /// <param name="right">The second color to multiply.</param>
    /// <returns>The result of multiplying the colors together.</returns>
    public static Color operator *(Color left, Color right)
    {
        return new Color(
            left.Red * right.Red,
            left.Green * right.Green,
            left.Blue * right.Blue,
            left.Alpha * right.Alpha);
    }

    /// <summary>
    /// Divide a color by a number.
    /// </summary>
    /// <param name="left">The color to divide.</param>
    /// <param name="right">The number to divide.</param>
    /// <returns>The result of dividing the color by the number.</returns>
    public static Color operator /(Color left, double right)
    {
        return new Color(
            left.Red / right,
            left.Green / right,
            left.Blue / right,
            left.Alpha / right);
    }
}
