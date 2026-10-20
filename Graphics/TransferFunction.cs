namespace RayTracer.Graphics;

/// <summary>
/// This class represents the curve an image's channel values were written with, and turns them back
/// into the light they stand for.
/// <para>
/// **A picture file does not hold light.**  Almost every photograph, painting and texture is written in
/// sRGB, whose values climb steeply through the darks and gently through the lights so that eight bits
/// spend themselves where an eye can tell them apart.  A mid-gray of 128 in such a file is about a fifth
/// of white's light, not half.  Shading works in light, so a file's values must be turned back into it
/// before anything is lit with them -- taken as they stand, every image came out paler and washed of its
/// color, a mid-gray rendering as 186.  The file's own word on how it was written is taken: a Radiance or
/// OpenEXR file holds light directly, and anything else declares its gamma.
/// </para>
/// <para>
/// Only an image whose values are colors is decoded.  A height field's gray is a height, so it is read
/// as it stands.
/// </para>
/// </summary>
public sealed class TransferFunction
{
    /// <summary>
    /// This field holds the transfer of values that are light already.
    /// </summary>
    public static readonly TransferFunction Linear = new ("linear", 1);

    /// <summary>
    /// This field holds the sRGB curve: a short straight run through the deepest darks and a 2.4 power
    /// above it, which together track a 2.2 power closely everywhere but the darks.
    /// </summary>
    public static readonly TransferFunction Srgb = new ("sRGB", double.NaN);

    /// <summary>
    /// This property holds the name of the curve, for messages.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// This property reports whether values come back from this curve as they went in.
    /// </summary>
    public bool IsLinear => _exponent == 1;

    // The power a value is raised to to recover its light, or NaN for the sRGB curve.
    private readonly double _exponent;

    private TransferFunction(string name, double exponent)
    {
        Name = name;
        _exponent = exponent;
    }

    /// <summary>
    /// This method returns the transfer of a file written with a plain power: a value written as light
    /// raised to the given gamma, 0.4545 for the common 2.2.
    /// </summary>
    /// <param name="gamma">The gamma the file was written with.</param>
    /// <returns>The matching transfer.</returns>
    public static TransferFunction Power(double gamma)
    {
        return Math.Abs(gamma - 1) < 0.01
            ? Linear
            : new TransferFunction($"gamma {gamma:0.####}", 1 / gamma);
    }

    /// <summary>
    /// This method turns one channel's value, as the file held it, back into light.
    /// </summary>
    /// <param name="value">The value, in [0, 1] for anything but a file holding light.</param>
    /// <returns>The light it stands for.</returns>
    public double ToLinear(double value)
    {
        if (_exponent == 1)
            return value;

        if (double.IsNaN(_exponent))
        {
            return value <= 0.04045
                ? value / 12.92
                : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return value <= 0 ? 0 : Math.Pow(value, _exponent);
    }

    /// <summary>
    /// This method turns one channel's light into the value a file written with this curve holds for it:
    /// the other way from <see cref="ToLinear"/>.
    /// </summary>
    /// <param name="light">The light, which may be more than one or less than nought; neither is clamped.</param>
    /// <returns>The value to write.</returns>
    public double FromLinear(double light)
    {
        if (_exponent == 1)
            return light;

        if (double.IsNaN(_exponent))
        {
            return light <= 0.0031308
                ? light * 12.92
                : 1.055 * Math.Pow(light, 1 / 2.4) - 0.055;
        }

        return light <= 0 ? 0 : Math.Pow(light, 1 / _exponent);
    }

    public override string ToString()
    {
        return Name;
    }
}
