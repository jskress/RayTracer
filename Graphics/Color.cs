using RayTracer.Extensions;
using RayTracer.General;

namespace RayTracer.Graphics;

/// <summary>
/// This class represents a color in the raytracer.
/// </summary>
public class Color
{
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
    /// <param name="context">The current rendering context.</param>
    /// <returns>A tuple containing the converted channel values.</returns>
    public (int Red, int Green, int Blue, int Alpha) ToChannelValues(RenderContext context)
    {
        bool gammaCorrect = context.ApplyGamma;
        double maxValue = Convert.ToDouble(context.MaxColorChannelValue);
        double power = gammaCorrect ? 1 / context.Gamma : 1;

        return (ChannelToInt(Red, maxValue, power, gammaCorrect),
            ChannelToInt(Green, maxValue, power, gammaCorrect),
            ChannelToInt(Blue, maxValue, power, gammaCorrect),
            // Note: the alpha channel is linear so we never gamma correct it.
            ChannelToInt(Alpha, maxValue));
    }

    /// <summary>
    /// This method returns this color as an appropriate shade of gray.
    /// </summary>
    /// <param name="context">The current rendering context.</param>
    /// <returns>A tuple containing the gray and alpha values.</returns>
    public (int Gray, int Alpha) ToGrayValue(RenderContext context)
    {
        bool gammaCorrect = context.ApplyGamma;
        double maxValue = Convert.ToDouble(context.MaxColorChannelValue);
        double power = gammaCorrect ? 1 / context.Gamma : 1;
        double gray = PrepareChannelValue(Red, power, gammaCorrect) * 0.299 +
                      PrepareChannelValue(Green, power, gammaCorrect) * 0.587 +
                      PrepareChannelValue(Green, power, gammaCorrect) * 0.114;

        return (ChannelToInt(gray, maxValue), ChannelToInt(Alpha, maxValue));
    }

    /// <summary>
    /// This is a helper method for converting a channel value to an integer, applying
    /// gamma correction upon request.
    /// </summary>
    /// <param name="value">The value to convert</param>
    /// <param name="maxValue">The maximum value the integer form can take.</param>
    /// <param name="power">The power factor to use when gamma correcting.</param>
    /// <param name="gammaCorrect">Whether to apply gamma correction.</param>
    /// <returns>The channel value converted to an integer.</returns>
    private static int ChannelToInt(
        double value, double maxValue, double power = 0, bool gammaCorrect = false)
    {
        return Convert.ToInt32(PrepareChannelValue(value, power, gammaCorrect) * maxValue);
    }

    /// <summary>
    /// This is a helper method for preparing a channel value for output by (conditionally)
    /// applying gamma correction and clamping the value into the appropriate range.
    /// </summary>
    /// <param name="value">The channel value to prepare.</param>
    /// <param name="power">The power factor to use when gamma correcting.</param>
    /// <param name="gammaCorrect">Whether to apply gamma correction.</param>
    /// <returns>The prepared channel value.</returns>
    private static double PrepareChannelValue(
        double value, double power = 1, bool gammaCorrect = false)
    {
        if (gammaCorrect && Math.Abs(power - 1) > 0.0000001)
        {
            value = Math.Pow(value, power);

            // For colors, this should be ok.
            if (double.IsNaN(value))
                value = 0;
        }

        return Math.Clamp(value, 0, 1);
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

    /// <summary>
    /// This method creates a copy of this color, replacing its current alpha value with
    /// the one provided.
    /// </summary>
    /// <param name="alpha">The new alpha value to use.</param>
    /// <returns>A copy of this color, with the new alpha value.</returns>
    public Color WithAlpha(double alpha)
    {
        return new Color(Red, Green, Blue, alpha);
    }

    /// <summary>
    /// This method is used to layer this color on top of another color.  The colors are
    /// combined by applying the alpha channel.  If this color has no transparency, this
    /// color is returned unaltered (since nothing of the other color can show through).
    /// </summary>
    /// <param name="other">The color to layer this one on top of.</param>
    /// <returns>The color resulting from layering this color on the one given.</returns>
    public Color LayerOnTopOf(Color other)
    {
        // If we have no alpha, then there's no layering possible.
        if (Alpha >= 1)
            return this;

        // If we are fully transparent, then the other color is good.
        if (Alpha == 0)
            return other; 

        double alpha = 1 - (1 - Alpha) * (1 - other.Alpha);
        double red = Red * Alpha / alpha + other.Red * other.Alpha * (1 - Alpha) / alpha;
        double green = Green * Alpha / alpha + other.Green * other.Alpha * (1 - Alpha) / alpha;
        double blue = Blue * Alpha / alpha + other.Blue * other.Alpha * (1 - Alpha) / alpha;

        return new Color(red, green, blue, alpha);
    }

    /// <summary>
    /// This method produces a string representation for the color.  It is intended for use
    /// in debugging so is very simplistic.
    /// </summary>
    /// <returns>A descriptive string that represents this color.</returns>
    public override string ToString()
    {
        string alpha = Alpha.Near(1)
            ? string.Empty
            : $", {Alpha}";
        return $"Color({Red}, {Green}, {Blue}{alpha})";
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

    /// <summary>
    /// Divide a color by a number.
    /// </summary>
    /// <param name="left">The color to multiply.</param>
    /// <param name="right">The number to multiply.</param>
    /// <returns>The result of multiplying the color by the number.</returns>
    public static Color operator /(Color left, double right)
    {
        return new Color(
            left.Red / right,
            left.Green / right,
            left.Blue / right);
    }
}
