using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class holds a surface's glints: its facets taken to be of a size, few enough to count, rather than so
/// small and so many that only their average shows -- the flakes in metallic paint, the ice grains of snow, the
/// quartz in sand, each a little mirror catching the sun.
/// <para>
/// **The facets under a pixel are counted, not averaged.**  A rough surface's highlight is the share of its facets
/// that lean the one way that mirrors a lamp into the eye, worked out as if there were infinitely many of them.
/// There are not: a pixel's patch of snow holds some hundreds of grains, and of those, the lamp's own sliver of
/// directions is caught by none -- or by one, which then shines as if the whole patch were mirror.  So the
/// number that lean the right way under the pixel is drawn, from a Poisson law whose mean is what the smooth
/// highlight assumes (Zirr and Kaplanyan, "Real-time Rendering of Procedural Multiscale Materials", 2016; the
/// count over a grid matched to the pixel, as Deliot and Belcour's "Real-Time Rendering of Glinty Appearances
/// using Distributed Binomial Laws on Anisotropic Grids", 2023, do), and the highlight scaled by how many were
/// found against how many were expected.  Near, a few pixels blaze and the rest are dark; far off, each pixel's
/// patch holds so many that the count is its mean, and the surface shows its smooth highlight, unchanged.
/// </para>
/// <para>
/// **The count belongs to the place**, so a glint stays where it is: it is drawn by hashing the cell of a grid,
/// laid in the space of the surface the material belongs to and as fine as the pixel's patch, and the cell of the
/// half-vector's own grid, as fine as the lamp is wide.  Every ray in a pixel's cell finds the same facets; move
/// the lamp or the eye and the half-vector moves into another cell, and other facets catch it.  Between two
/// sizes of the grid the two counts are blended, so a surface running away from the eye has no seam.
/// </para>
/// <para>
/// Only lamps glint: the sun, lamps, the patches of an area light.  What the surface mirrors of the rest of the
/// world keeps the smooth highlight's blur.
/// </para>
/// </summary>
public class Glints
{
    /// <summary>
    /// This property holds how big a facet is, across, in the world: a flake of paint a few hundredths of a
    /// millimeter, a grain of snow a millimeter.
    /// </summary>
    public double Size { get; set; }

    /// <summary>
    /// This property holds how far the facets lean, for glints on a surface that is not rough -- grains of their
    /// own on a matte skin.  A rough surface's glints are its own facets, which lean as its roughness says.
    /// </summary>
    public double? Roughness { get; set; }

    /// <summary>
    /// How far the grains on a surface that is not rough lean, unless said.
    /// </summary>
    internal const double GrainRoughness = 0.5;

    /// <summary>
    /// What the grains on a surface that is not rough mirror square-on: what nearly everything that is not a
    /// metal does, ice and quartz among them.
    /// </summary>
    internal const double GrainReflectance = 0.04;

    // The angular radius taken for a lamp that has none -- a point, or a direction -- in radians: the sun's.
    private const double SmallestLamp = 0.00465;

    // How many facets may be expected to catch a lamp before the count is its mean to well within a percent, and the
    // smooth highlight answers as well.
    private const double Plenty = 10000;

    // Below this mean the count is drawn by summing its terms; above, from the normal law it approaches.
    private const double Summed = 64;

    /// <summary>
    /// This property holds how far the grains of a surface that is not rough lean: the square of their roughness.
    /// </summary>
    internal double GrainAlpha
    {
        get
        {
            double roughness = Roughness ?? GrainRoughness;

            return roughness * roughness;
        }
    }

    /// <summary>
    /// This method returns how much of the smooth highlight a lamp gets from the facets under a pixel: how many of
    /// them lean so as to mirror it into the eye, against how many would on average.  Nought, mostly, where
    /// few are expected, and now and then many times one; one, on average, and always where many are.
    /// </summary>
    /// <param name="local">Where the surface is being lit, in the space its material is laid in.</param>
    /// <param name="footprint">The patch of the surface the pixel covers there, in the world.</param>
    /// <param name="footprintThere">The same patch, in the space the material is laid in.</param>
    /// <param name="normal">The way out of the surface, in the world, of unit length.</param>
    /// <param name="halfway">The halfway between the eye and the lamp, in the world, of unit length.</param>
    /// <param name="alpha">How far the facets lean.</param>
    /// <param name="spread">The angle across the patch of the lamp the sample stands for, in radians; nought for a
    /// lamp that is a point or a direction.</param>
    /// <returns>The share of the smooth highlight the lamp gets.</returns>
    internal double ShareAt(
        Point local, Footprint footprint, Footprint footprintThere, Vector normal, Vector halfway, double alpha,
        double spread)
    {
        double cosHalfway = normal.Dot(halfway);

        if (footprint is null || footprint.IsEmpty || footprintThere is null || footprintThere.IsEmpty ||
            cosHalfway <= 0)
            return 1;

        double area = footprint.Across.Cross(footprint.Along).Magnitude;
        double areaThere = footprintThere.Across.Cross(footprintThere.Along).Magnitude;

        if (!(area > 0) || !(areaThere > 0))
            return 1;

        // The half-vector's directions that mirror some of the lamp lie within half its angular radius of the
        // halfway: a facet turned by an angle turns what it mirrors by twice it.
        double cone = Math.Max(spread / 2, SmallestLamp) / 2;
        double expected = area / (Size * Size) * Microfacets.Distribution(cosHalfway, alpha) * cosHalfway *
                          Math.PI * cone * cone;

        if (expected >= Plenty)
            return 1;

        if (!(expected > 0))
            return 0;

        (Vector across, Vector along) = Microfacets.FrameAround(normal);
        long slopeX = (long) Math.Floor(halfway.Dot(across) / cosHalfway / (2 * cone));
        long slopeY = (long) Math.Floor(halfway.Dot(along) / cosHalfway / (2 * cone));

        // A facet's size in the space the grid is laid in, and how many sizes of facet across the pixel's patch is.
        double size = Size * Math.Sqrt(areaThere / area);
        double level = Math.Log2(Math.Sqrt(area) / Size);

        if (level <= 0)
            return Counted(local, size, 0, slopeX, slopeY, expected) / expected;

        int lower = (int) Math.Floor(level);
        double between = level - lower;

        return ((1 - between) * Counted(local, size, lower, slopeX, slopeY, expected) +
                between * Counted(local, size, lower + 1, slopeX, slopeY, expected)) / expected;
    }

    /// <summary>
    /// This method draws how many facets catch the lamp in one cell of the grid at one size, for one cell of the
    /// half-vector's grid.
    /// </summary>
    private static double Counted(Point local, double size, int level, long slopeX, long slopeY, double expected)
    {
        double cell = size * Math.Pow(2, level);
        ulong bits = Mix(0x9E3779B97F4A7C15UL * (ulong) (level + 1));

        bits = Mix(bits ^ (ulong) (long) Math.Floor(local.X / cell));
        bits = Mix(bits ^ (ulong) (long) Math.Floor(local.Y / cell));
        bits = Mix(bits ^ (ulong) (long) Math.Floor(local.Z / cell));
        bits = Mix(bits ^ (ulong) slopeX);
        bits = Mix(bits ^ (ulong) slopeY);

        return Poisson(expected, (bits >> 11) * (1.0 / (1UL << 53)));
    }

    /// <summary>
    /// This method returns the number a Poisson law of the given mean gives for a draw from nought to one: the
    /// least count whose chance of being reached takes in the draw.
    /// </summary>
    internal static double Poisson(double mean, double draw)
    {
        if (mean > Summed)
            return Math.Max(0, Math.Round(mean + Math.Sqrt(mean) * Normal(draw)));

        double term = Math.Exp(-mean);
        double sum = term;
        int count = 0;

        while (draw > sum && term > 0)
        {
            count++;
            term *= mean / count;
            sum += term;
        }

        return count;
    }

    /// <summary>
    /// This method returns where the standard normal law reaches the given share: Acklam's rational
    /// approximation, good to about one part in a billion.
    /// </summary>
    private static double Normal(double share)
    {
        share = Math.Clamp(share, 1e-12, 1 - 1e-12);

        const double Low = 0.02425;

        if (share < Low)
        {
            double q = Math.Sqrt(-2 * Math.Log(share));

            return Tail(q);
        }

        if (share > 1 - Low)
        {
            double q = Math.Sqrt(-2 * Math.Log(1 - share));

            return -Tail(q);
        }

        double r = share - 0.5;
        double s = r * r;

        return (((((-3.969683028665376e+01 * s + 2.209460984245205e+02) * s - 2.759285104469687e+02) * s +
                   1.383577518672690e+02) * s - 3.066479806614716e+01) * s + 2.506628277459239e+00) * r /
               (((((-5.447609879822406e+01 * s + 1.615858368580409e+02) * s - 1.556989798598866e+02) * s +
                  6.680131188771972e+01) * s - 1.328068155288572e+01) * s + 1);

        static double Tail(double q) =>
            (((((-7.784894002430293e-03 * q - 3.223964580411365e-01) * q - 2.400758277161838e+00) * q -
               2.549732539343734e+00) * q + 4.374664141464968e+00) * q + 2.938163982698783e+00) /
            ((((7.784695709041462e-03 * q + 3.224671290700398e-01) * q + 2.445134137142996e+00) * q +
              3.754408661907416e+00) * q + 1);
    }

    /// <summary>
    /// This method stirs a number's bits, as splitmix64 does.
    /// </summary>
    private static ulong Mix(ulong bits)
    {
        bits += 0x9E3779B97F4A7C15UL;
        bits = (bits ^ (bits >> 30)) * 0xBF58476D1CE4E5B9UL;
        bits = (bits ^ (bits >> 27)) * 0x94D049BB133111EBUL;

        return bits ^ (bits >> 31);
    }
}
