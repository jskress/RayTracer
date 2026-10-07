using RayTracer.Graphics;

namespace RayTracer.Terms;

/// <summary>
/// This class holds the functions that make colors.
/// </summary>
public static class ColorFunctions
{
    /// <summary>
    /// This function returns the color of something glowing at the given temperature, in kelvin: a
    /// candle about 1900, a household bulb 2700, noon sunlight about 5500, which is white here.  Its
    /// brightest channel is one, as a light given a <c>temperature</c> is, so it may be multiplied to
    /// make it brighter.
    /// <para>
    /// It is for whatever glows without being a light -- a filament seen through glass, the stuff
    /// inside a lamp that lights the room as a volume, an ember.  A light given a <c>temperature</c>
    /// carries the glow's spectrum to a spectral render as well; a color made here is turned into a
    /// spectrum the way any color is, which for a glow comes out within a percent or so of the real
    /// thing.
    /// </para>
    /// </summary>
    /// <param name="temperature">How hot the glowing thing is, in kelvin.</param>
    /// <returns>The color it glows.</returns>
    [Function("kelvin")]
    public static Color Kelvin(double temperature)
    {
        if (temperature <= 0)
        {
            throw new ArgumentException(
                "A temperature is in kelvin, and nothing glows at absolute zero or below.");
        }

        return SpectralColor.GlowAt(temperature).Color;
    }
}
