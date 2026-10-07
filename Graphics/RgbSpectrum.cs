using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This struct holds light in three broad bands, red, green and blue, which is how this renderer
/// has always carried it and is still how it carries it unless asked to do otherwise.
/// <para>
/// **It is <see cref="Color"/>'s arithmetic, to the bit.**  Every operator here does exactly what
/// <see cref="Color"/>'s does, in the same order, and leaves the fourth number at one just as that does,
/// so that a scene shaded in this comes out as it did when shading was done in colors.  That is not
/// something to improve on: the gallery rendering unchanged is the proof that nothing was lost in
/// moving the shading over, and a sum regrouped for neatness would shift the odd pixel and spoil it.
/// </para>
/// <para>
/// Every way of turning a color into this is the same, the numbers as written.  Three bands hold no
/// more than a color does, so there is nothing for the difference between a surface, a lamp and a
/// rate to decide.
/// </para>
/// </summary>
public struct RgbSpectrum : ISpectrum<RgbSpectrum>
{
    /// <summary>
    /// This property holds the amount in the red band.
    /// </summary>
    public double Red { get; private set; }

    /// <summary>
    /// This property holds the amount in the green band.
    /// </summary>
    public double Green { get; private set; }

    /// <summary>
    /// This property holds the amount in the blue band.
    /// </summary>
    public double Blue { get; private set; }

    /// <summary>
    /// This property holds how much of its pixel this light covers.
    /// </summary>
    public double Alpha { get; private set; }

    public static int Count => 3;
    public static bool IsSpectral => false;
    public static RgbSpectrum Black => new (0, 0, 0);
    public static RgbSpectrum White => new (1, 1, 1);

    public RgbSpectrum(double red, double green, double blue, double alpha = 1)
    {
        Red = red;
        Green = green;
        Blue = blue;
        Alpha = alpha;
    }

    /// <summary>
    /// This method takes a color's numbers as they are, which is what every one of the ways of
    /// turning a color into three bands comes to.
    /// </summary>
    /// <param name="color">The color to take.</param>
    /// <returns>The same numbers, as light.</returns>
    public static RgbSpectrum From(Color color)
    {
        return new RgbSpectrum(color.Red, color.Green, color.Blue, color.Alpha);
    }

    public static RgbSpectrum FromReflectance(Color color) => From(color);
    public static RgbSpectrum FromIlluminant(Color color) => From(color);
    public static RgbSpectrum FromUnbounded(Color color) => From(color);
    public static RgbSpectrum FromSampled(ReadOnlySpan<double> perBand) => From(SpectralColor.ToColor(perBand));

    public double this[int band]
    {
        readonly get => band switch
        {
            0 => Red,
            1 => Green,
            2 => Blue,
            _ => throw new ArgumentOutOfRangeException(nameof(band))
        };
        set
        {
            switch (band)
            {
                case 0:
                    Red = value;
                    break;
                case 1:
                    Green = value;
                    break;
                case 2:
                    Blue = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(band));
            }
        }
    }

    public readonly RgbSpectrum WithAlpha(double alpha) => new (Red, Green, Blue, alpha);

    public readonly bool IsBlack =>
        Red.Near(0) && Green.Near(0) && Blue.Near(0) && Alpha.Near(1);

    public readonly double Sum => Red + Green + Blue;

    public readonly double Average => (Red + Green + Blue) / 3;

    public readonly Color ToColor() => new (Red, Green, Blue, Alpha);

    public override readonly string ToString()
    {
        return ToColor().ToString();
    }

    public static RgbSpectrum operator +(RgbSpectrum left, RgbSpectrum right)
    {
        return new RgbSpectrum(
            left.Red + right.Red,
            left.Green + right.Green,
            left.Blue + right.Blue);
    }

    public static RgbSpectrum operator -(RgbSpectrum left, RgbSpectrum right)
    {
        return new RgbSpectrum(
            left.Red - right.Red,
            left.Green - right.Green,
            left.Blue - right.Blue);
    }

    public static RgbSpectrum operator *(RgbSpectrum left, RgbSpectrum right)
    {
        return new RgbSpectrum(
            left.Red * right.Red,
            left.Green * right.Green,
            left.Blue * right.Blue);
    }

    public static RgbSpectrum operator *(RgbSpectrum left, double right)
    {
        return new RgbSpectrum(
            left.Red * right,
            left.Green * right,
            left.Blue * right);
    }

    public static RgbSpectrum operator *(double left, RgbSpectrum right)
    {
        return new RgbSpectrum(
            left * right.Red,
            left * right.Green,
            left * right.Blue);
    }

    public static RgbSpectrum operator /(RgbSpectrum left, double right)
    {
        return new RgbSpectrum(
            left.Red / right,
            left.Green / right,
            left.Blue / right);
    }
}
