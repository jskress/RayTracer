namespace RayTracer.Graphics;

/// <summary>
/// This class represents how the light a render ends with is developed into a picture: what becomes of light
/// brighter than a screen's white.
/// <para>
/// **A screen stops at white, and light does not.**  Cut off at white channel by channel, as every render was
/// until 2026-10-19, anything brighter loses its shading: a lit highlight goes flat at the point it runs over.
/// Film and a camera's sensor do otherwise: they take light beyond a point less and less, so it climbs toward
/// white without arriving and keeps its shading on the way.
/// </para>
/// <para>
/// **Soft, the default, eases each channel toward white by itself**, as each of a color film's layers takes its
/// own color, and changes nothing below eight tenths of white, so a scene's colors come out as they were written.
/// A bright orange runs through yellow to white on the way, because its red is eased first -- which is what a
/// flame does in a photograph.  Neutral eases only the brightest channel and pales the color to match, keeping
/// its hue (Khronos's PBR Neutral, without the toe it adds to deepen the darks): it was the default for a day,
/// and turned every flame peach and every sunset beige.  AgX is a film's curve over the whole range, softer and
/// less saturated, with room for light sixteen times brighter than middle gray; it wants a scene exposed for it.
/// None cuts off at white, as every render did until 2026-10-19.
/// </para>
/// </summary>
public sealed class FilmCurve
{
    /// <summary>
    /// This field holds the curve that cuts light off at white, channel by channel.
    /// </summary>
    public static readonly FilmCurve None = new (
        "none", (red, green, blue) => (Math.Min(red, 1), Math.Min(green, 1), Math.Min(blue, 1)));

    /// <summary>
    /// This field holds the curve that leaves each channel below eight tenths of white alone and eases what is
    /// brighter toward white, channel by channel.
    /// </summary>
    public static readonly FilmCurve Soft = new (
        "soft", (red, green, blue) => (Eased(red), Eased(green), Eased(blue)));

    /// <summary>
    /// This field holds the curve that leaves light below eight tenths of white alone and eases what is
    /// brighter toward white, keeping its hue and paling it.
    /// </summary>
    public static readonly FilmCurve Neutral = new ("neutral", RollOff);

    /// <summary>
    /// This field holds Troy Sobotka's AgX, a film's curve over the whole range.
    /// </summary>
    public static readonly FilmCurve AgX = new ("agx", Agx);

    // Where the neutral curve starts to ease the light, and how quickly it pales what it eases.
    private const double Knee = 0.8;
    private const double Paling = 0.15;

    // AgX takes light between these many stops below and above middle gray.
    private const double LeastStops = -12.47393;
    private const double MostStops = 4.026069;

    // Linear sRGB to Rec. 2020 and back, and AgX's own inset and outset, each by rows.
    private static readonly double[,] ToRec2020 =
    {
        { 0.6274, 0.3293, 0.0433 }, { 0.0691, 0.9195, 0.0113 }, { 0.0164, 0.0880, 0.8956 }
    };
    private static readonly double[,] FromRec2020 =
    {
        { 1.6605, -0.5876, -0.0728 }, { -0.1246, 1.1329, -0.0083 }, { -0.0182, -0.1006, 1.1187 }
    };
    private static readonly double[,] Inset =
    {
        { 0.856627153315983, 0.0951212405381588, 0.0482516061458583 },
        { 0.137318972929847, 0.761241990602591, 0.101439036467562 },
        { 0.11189821299995, 0.0767994186031903, 0.811302368396859 }
    };
    private static readonly double[,] Outset =
    {
        { 1.1271005818144368, -0.11060664309660323, -0.016493938717834573 },
        { -0.1413297634984383, 1.157823702216272, -0.016493938717834257 },
        { -0.14132976349843826, -0.11060664309660294, 1.2519364065950405 }
    };

    /// <summary>
    /// This property holds the curve's name, as a scene says it.
    /// </summary>
    public string Name { get; }

    private readonly Func<double, double, double, (double, double, double)> _develop;

    private FilmCurve(string name, Func<double, double, double, (double, double, double)> develop)
    {
        Name = name;
        _develop = develop;
    }

    /// <summary>
    /// This method returns the curve with the given name.
    /// </summary>
    /// <param name="name">The name, as a scene says it.</param>
    /// <returns>The curve.</returns>
    public static FilmCurve Named(string name)
    {
        return name switch
        {
            "none" => None,
            "soft" => Soft,
            "neutral" => Neutral,
            "agx" => AgX,
            _ => throw new ArgumentException($"There is no film curve named {name}; it may be soft, neutral, agx or none.")
        };
    }

    /// <summary>
    /// This method develops light into what a picture shows of it: light again, but at most white, for the
    /// curve a file is written with to finish.  Alpha is left as it was.
    /// </summary>
    /// <param name="light">The light.</param>
    /// <returns>What the picture shows.</returns>
    public Color Develop(Color light)
    {
        (double red, double green, double blue) = _develop(Real(light.Red), Real(light.Green), Real(light.Blue));

        return new Color(red, green, blue, light.Alpha);
    }

    // Nothing is less than no light.
    private static double Real(double value) => double.IsNaN(value) ? 0 : Math.Max(value, 0);

    // One channel eased toward white above the knee, as the neutral curve eases its brightest.
    private static double Eased(double value)
    {
        const double room = 1 - Knee;

        return value < Knee ? value : 1 - room * room / (value + room - Knee);
    }

    private static (double, double, double) RollOff(double red, double green, double blue)
    {
        double peak = Math.Max(red, Math.Max(green, blue));

        if (peak < Knee)
            return (red, green, blue);

        const double room = 1 - Knee;
        double eased = 1 - room * room / (peak + room - Knee);
        double scale = eased / peak;
        double pale = 1 - 1 / (Paling * (peak - eased) + 1);

        red *= scale;
        green *= scale;
        blue *= scale;

        return (red + (eased - red) * pale, green + (eased - green) * pale, blue + (eased - blue) * pale);
    }

    private static (double, double, double) Agx(double red, double green, double blue)
    {
        (red, green, blue) = Times(ToRec2020, red, green, blue);
        (red, green, blue) = Times(Inset, red, green, blue);
        (red, green, blue) = Times(Outset, Sigmoid(red), Sigmoid(green), Sigmoid(blue));

        // What the sigmoid gives is for a 2.2 screen; it is turned back into light for the file's own curve.
        (red, green, blue) = Times(
            FromRec2020, Math.Pow(Math.Max(red, 0), 2.2), Math.Pow(Math.Max(green, 0), 2.2),
            Math.Pow(Math.Max(blue, 0), 2.2));

        return (Math.Clamp(red, 0, 1), Math.Clamp(green, 0, 1), Math.Clamp(blue, 0, 1));
    }

    // AgX's default contrast, a polynomial fitted to its sigmoid, over the light's stops from the least to the
    // most it takes.
    private static double Sigmoid(double light)
    {
        double x = Math.Clamp((Math.Log2(Math.Max(light, 1e-10)) - LeastStops) / (MostStops - LeastStops), 0, 1);
        double x2 = x * x, x4 = x2 * x2;

        return 15.5 * x4 * x2 - 40.14 * x4 * x + 31.96 * x4 - 6.868 * x2 * x + 0.4298 * x2 + 0.1191 * x - 0.00232;
    }

    private static (double, double, double) Times(double[,] matrix, double red, double green, double blue)
    {
        return (
            matrix[0, 0] * red + matrix[0, 1] * green + matrix[0, 2] * blue,
            matrix[1, 0] * red + matrix[1, 1] * green + matrix[1, 2] * blue,
            matrix[2, 0] * red + matrix[2, 1] * green + matrix[2, 2] * blue);
    }

    public override string ToString()
    {
        return Name;
    }
}
