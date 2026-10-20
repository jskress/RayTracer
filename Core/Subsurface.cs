using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class describes light that goes into a surface, wanders about under it and comes out again
/// somewhere else: wax, marble, jade, milk, skin.
/// <para>
/// **The color you see is the pigment's, and how far the light gets inside is the radius.**  A surface of
/// this kind is lit not where the light lands but where it comes out, after being turned over and over by the
/// stuff it is made of; how much survives the wandering is what color the surface looks, and how far it
/// wanders is what makes it look soft -- light bleeding round a candle's rim, glowing through an ear.  The
/// radius may be one distance or one for each of red, green and blue: skin carries red furthest.  It is in
/// the scene's own units.
/// </para>
/// <para>
/// **The color is worked back into the stuff that makes it.**  What a surface shows is not how much light
/// one turn inside it keeps -- that is far more, since light kept at each of many turns is lost little by
/// little -- so the color the scene asks for is turned into the share kept at each turn that gives it.  The
/// table here is the answer for exactly the walk <see cref="Scene"/> takes: into a deep slab, the way
/// spread as a matte surface spreads it, turned evenly every way at each turn, out at the first crossing
/// back.  It was worked out by following a million such walks and counting their turns.  It agrees with
/// Chiang, Kutz and Burley's fit (2016) to within 0.16% for colors of 0.3 and up, and 0.5% at 0.2; darker,
/// it keeps a little less than theirs (1.8% less at 0.1, 2.8% at 0.05), their fit being made for their walk.
/// </para>
/// </summary>
public sealed class Subsurface
{
    // The square root of one less the share kept at each turn, for colors from nought by sixty-fourths: it
    // runs nearly straight even toward white, where the share itself climbs ever more steeply to one.
    private static readonly double[] Table =
    [
        1.000000, 0.962655, 0.927033, 0.893007, 0.860461, 0.829293, 0.799409, 0.770725,
        0.743165, 0.716658, 0.691140, 0.666555, 0.642846, 0.619967, 0.597869, 0.576513,
        0.555858, 0.535868, 0.516511, 0.497753, 0.479567, 0.461924, 0.444799, 0.428168,
        0.412008, 0.396299, 0.381020, 0.366153, 0.351680, 0.337585, 0.323851, 0.310465,
        0.297412, 0.284679, 0.272253, 0.260123, 0.248278, 0.236706, 0.225399, 0.214345,
        0.203537, 0.192965, 0.182621, 0.172498, 0.162587, 0.152882, 0.143375, 0.134061,
        0.124932, 0.115983, 0.107207, 0.098600, 0.090156, 0.081870, 0.073738, 0.065755,
        0.057918, 0.050223, 0.042668, 0.035250, 0.027964, 0.020804, 0.013753, 0.006333,
    ];

    /// <summary>
    /// This property holds how far light travels inside before it is turned, on average, in the scene's
    /// units: one distance, or one for each of red, green and blue.
    /// </summary>
    public Color Radius { get; set; } = new (1, 1, 1);

    /// <summary>
    /// This property holds how thick a sheet of the stuff is -- a leaf, a sheet of paper, a lampshade -- in
    /// the scene's units, or null to take it as thick as the light travels inside, the average of the radius.
    /// A sheet has no inside of its own to walk in, so its light is walked through a slab this thick; a
    /// closed surface walks through its own inside and has no use for it.
    /// </summary>
    public double? Thickness { get; set; }

    /// <summary>
    /// This property returns how thick a sheet of the stuff is taken to be.
    /// </summary>
    public double SheetThickness => Thickness ?? (Radius.Red + Radius.Green + Radius.Blue) / 3;

    /// <summary>
    /// This method returns the share of light kept at each turn inside that makes a deep slab of the stuff
    /// show the given color, from nought up to just short of one.
    /// </summary>
    /// <param name="color">The color, one channel of it, from nought to one.</param>
    /// <returns>The share kept at each turn.</returns>
    public static double AlbedoFor(double color)
    {
        if (double.IsNaN(color) || color <= 0)
            return 0;

        double place = Math.Min(color, 1) * Table.Length;
        int below = Math.Min((int) place, Table.Length - 1);
        double above = below + 1 < Table.Length ? Table[below + 1] : 0;
        double root = Table[below] + (above - Table[below]) * (place - below);

        return 1 - root * root;
    }

    /// <summary>
    /// This method returns the share of light kept at each turn, band by band, that makes the stuff show the
    /// given color.
    /// </summary>
    public static TS AlbedoOf<TS>(TS color)
        where TS : struct, ISpectrum<TS>
    {
        TS albedo = TS.Black;

        for (int band = 0; band < TS.Count; band++)
            albedo[band] = AlbedoFor(color[band]);

        return albedo;
    }

    /// <summary>
    /// This method returns how often light is turned inside, band by band: once in every radius it travels.
    /// </summary>
    public TS ExtinctionOf<TS>()
        where TS : struct, ISpectrum<TS>
    {
        return TS.FromUnbounded(new Color(1 / Radius.Red, 1 / Radius.Green, 1 / Radius.Blue));
    }
}
