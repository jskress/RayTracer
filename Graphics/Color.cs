namespace RayTracer.Graphics;

/// <summary>
/// This class represents a color in the raytracer.
/// </summary>
public class Color
{
    internal static Color FromUint(uint rawValue)
    {
        int red = (int) ((rawValue & 0x00ff0000) >> 0x10);
        int green = (int) ((rawValue & 0x0000ff00) >> 0x8);
        int blue = (int) (rawValue & 0x000000ff);
        int alpha = (int) ((rawValue & 0xff000000) >> 0x18);

        return new Color(red / 255.0d, green / 255.0d, blue / 255.0d, alpha / 255.0d);
    }

    /// <summary>
    /// This method creates a color from integer channel values.
    /// </summary>
    /// <param name="red">The channel value for red.</param>
    /// <param name="green">The channel value for green.</param>
    /// <param name="blue">The channel value for blue.</param>
    /// <param name="maxValue">The maximum value for a channel.</param>
    /// <returns>The resulting color.</returns>
    public static Color FromChannelValues(int red, int green, int blue, int maxValue)
    {
        double max = Convert.ToDouble(maxValue);

        return new Color(red / max, green / max, blue / max);
    }

    /// <summary>
    /// This method creates a color from integer channel values.
    /// </summary>
    /// <param name="red">The channel value for red.</param>
    /// <param name="green">The channel value for green.</param>
    /// <param name="blue">The channel value for blue.</param>
    /// <param name="alpha">The channel value for alpha.</param>
    /// <param name="maxValue">The maximum value for a channel.</param>
    /// <returns>The resulting color.</returns>
    public static Color FromChannelValues(int red, int green, int blue, int alpha, int maxValue)
    {
        double max = Convert.ToDouble(maxValue);

        return new Color(red / max, green / max, blue / max, alpha / max);
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

    public Color(double red, double green, double blue, double alpha = 1)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    /// <summary>
    /// This method returns the 4 color channel values as integers in a range from 0 to
    /// the currently configured largest color channel value.
    /// </summary>
    /// <param name="gammaCorrect">Whether gamma correction should be applied.</param>
    /// <returns>A tuple containing the converted channel values.</returns>
    public (int Red, int Green, int Blue, int Alpha) ToChannelValues(bool gammaCorrect = true)
    {
        double maxValue = Convert.ToDouble(ProgramOptions.Instance.MaxColorChannelValue);
        double power = gammaCorrect ? 1 / ProgramOptions.Instance.Gamma : 1;

        return (ChannelToInt(Red, power, gammaCorrect, maxValue),
            ChannelToInt(Green, power, gammaCorrect, maxValue),
            ChannelToInt(Blue, power, gammaCorrect, maxValue),
            ChannelToInt(Alpha, power, gammaCorrect, maxValue));
    }

    /// <summary>
    /// This is a helper method for converting a channel value to an integer, applying
    /// gamma correction upon request.
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <param name="power">The power factor to use when gamma correcting.</param>
    /// <param name="gammaCorrect">Whether to apply gamma correction.</param>
    /// <param name="maxValue">The maximum value the integer form can take.</param>
    /// <returns>The channel value converted to an integer.</returns>
    private static int ChannelToInt(
        double value, double power, bool gammaCorrect, double maxValue)
    {
        if (gammaCorrect)
            value = Math.Pow(value, power);

        return Convert.ToInt32(Math.Clamp(value, 0, 1) * maxValue);
    }

    /// <summary>
    /// This method returns whether the given color matches this one.  This will be true
    /// if all members are equitable within a small tolerance.
    /// </summary>
    /// <param name="other">The color to compare to.</param>
    /// <returns><c>true</c>, if the two colors match, or <c>false</c>, if not.</returns>
    public bool Matches(Color other)
    {
        return Red.Near(other.Red) && Green.Near(other.Green) &&
               Blue.Near(other.Blue) && Alpha.Near(other.Alpha);
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
            left.Blue + right.Blue);
    }

    /// <summary>
    /// Subtract two colors.
    /// </summary>
    /// <param name="left">The left color to subtract from.</param>
    /// <param name="right">The right color to subtract.</param>
    /// <returns>The difference of the colors.</returns>
    public static Color operator -(Color left, Color right)
    {
        return new Color(
            left.Red - right.Red,
            left.Green - right.Green,
            left.Blue - right.Blue);
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
            left.Blue * right);
    }

    /// <summary>
    /// Multiply a color by a number.
    /// </summary>
    /// <param name="left">The number to multiply.</param>
    /// <param name="right">The color to multiply.</param>
    /// <returns>The result of multiplying the color by the number.</returns>
    public static Color operator *(double left, Color right)
    {
        return new Color(
            left * right.Red,
            left * right.Green,
            left * right.Blue);
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
            left.Blue * right.Blue);
    }
}
