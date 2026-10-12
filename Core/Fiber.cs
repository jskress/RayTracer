using System.Collections.Concurrent;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class describes how a strand of hair scatters light, after Chiang, Bitterli, Tappan and Burley
/// (2016) as PBRT implements it.
/// <para>
/// **A hair is a glassy cylinder with a colored inside**, and light leaves it in three ways that each
/// look different: some is mirrored off the outside (R, the white highlight), some passes through and
/// out the far side (TT, the glow of backlit hair), and some goes in, is mirrored off the inside, and
/// comes back out (TRT, the colored second highlight); what is left after more bounces inside is
/// gathered into a fourth, spread evenly around.  Each is the product of three things: how much
/// survives the trip (Fresnel at each surface, and the absorption of the pigment inside along the path
/// through it), how it spreads along the strand (a lobe about the mirror direction, shifted by the tilt
/// of the scales a hair's surface is made of), and how it spreads around it (a lobe about the way a ray
/// through a circle at that offset leaves it).
/// </para>
/// <para>
/// Everything here is in the strand's own frame: the strand runs along x, the eye looks back along a
/// direction whose part square to the strand is z, and y is z × x.  An angle θ is measured from the
/// plane square to the strand, φ around it from y, and <c>h</c>, from -1 to 1, is where across the
/// strand the eye's ray passed, positive toward y.
/// </para>
/// </summary>
public sealed partial class Fiber
{
    /// <summary>
    /// This is the index of refraction of hair, which is keratin.
    /// </summary>
    public const double Index = 1.55;

    /// <summary>
    /// This is how rough a strand is when nothing says otherwise: PBRT's, and about what real hair
    /// measures.
    /// </summary>
    public const double DefaultRoughness = 0.3;

    // How far the scales on a hair's surface tilt it, toward the root: two degrees.
    private static readonly double Tilt = 2 * Math.PI / 180;

    // How many lobes are worked out on their own; whatever is left over after them is the last.
    private const int Lobes = 3;

    // How much each unit of either melanin absorbs, in red, green and blue: d'Eon, Francois, Hill,
    // Letteri and Aubry (2011), as PBRT has them.
    private static readonly Color EumelaninAbsorption = new (0.419, 0.697, 1.37);
    private static readonly Color PheomelaninAbsorption = new (0.187, 0.4, 1.05);

    private static readonly ConcurrentDictionary<double, Fiber> Known = new ();

    private readonly double[] _variance = new double[Lobes + 1];
    private readonly double _spread;
    private readonly double[] _sinTilt = new double[3];
    private readonly double[] _cosTilt = new double[3];

    /// <summary>
    /// This property holds how rough the strand is, along it and around it alike, from nought to one.
    /// </summary>
    public double Roughness { get; }

    /// <summary>
    /// This method returns the fiber of the given roughness, made once and shared.
    /// </summary>
    /// <param name="roughness">How rough, from nought to one.</param>
    /// <returns>The fiber.</returns>
    public static Fiber For(double roughness) => Known.GetOrAdd(roughness, value => new Fiber(value));

    private Fiber(double roughness)
    {
        Roughness = roughness;

        // How widely each lobe spreads along the strand, from its roughness by Chiang et al.'s fit; light
        // that has passed through spreads less than light mirrored off the outside, and light mirrored
        // inside more.
        double root = 0.726 * roughness + 0.812 * roughness * roughness + 3.7 * Math.Pow(roughness, 20);

        _variance[0] = root * root;
        _variance[1] = 0.25 * _variance[0];
        _variance[2] = 4 * _variance[0];
        _variance[3] = _variance[2];

        // And how widely around it, by theirs again.
        _spread = Math.Sqrt(Math.PI / 8) *
                  (0.265 * roughness + 1.194 * roughness * roughness + 5.372 * Math.Pow(roughness, 22));

        // The tilt each lobe is shifted by: twice it, once, and four times.
        _shares = new Lazy<ShareTable>(() => new ShareTable(this));

        _sinTilt[0] = Math.Sin(Tilt);
        _cosTilt[0] = Math.Sqrt(1 - _sinTilt[0] * _sinTilt[0]);

        for (int index = 1; index < 3; index++)
        {
            _sinTilt[index] = 2 * _cosTilt[index - 1] * _sinTilt[index - 1];
            _cosTilt[index] = _cosTilt[index - 1] * _cosTilt[index - 1] - _sinTilt[index - 1] * _sinTilt[index - 1];
        }
    }

    /// <summary>
    /// This struct holds what a strand's scattering toward the eye owes to the eye's direction alone,
    /// worked out once for every light that reaches the same place.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    public readonly struct Seen<TS>
        where TS : struct, ISpectrum<TS>
    {
        internal readonly double SinThetaO;
        internal readonly double CosThetaO;
        internal readonly double PhiO;
        internal readonly double GammaO;
        internal readonly double GammaT;
        internal readonly TS Mirrored;
        internal readonly TS Through;
        internal readonly TS MirroredInside;
        internal readonly TS Rest;
        internal readonly TS Absorption;

        internal Seen(
            double sinThetaO, double cosThetaO, double phiO, double gammaO, double gammaT, TS mirrored,
            TS through, TS mirroredInside, TS rest, TS absorption)
        {
            Absorption = absorption;
            SinThetaO = sinThetaO;
            CosThetaO = cosThetaO;
            PhiO = phiO;
            GammaO = gammaO;
            GammaT = gammaT;
            Mirrored = mirrored;
            Through = through;
            MirroredInside = mirroredInside;
            Rest = rest;
        }
    }

    /// <summary>
    /// This method works out what a strand's scattering toward the eye owes to the eye's direction.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="sinThetaO">The sine of the eye's angle out of the plane square to the strand.</param>
    /// <param name="phiO">The eye's angle around the strand.</param>
    /// <param name="h">Where across the strand the eye's ray passed, from -1 to 1.</param>
    /// <param name="absorption">How much the strand's inside absorbs, per band, over its own radius.</param>
    /// <returns>What the scattering toward the eye owes to its direction.</returns>
    public Seen<TS> Seeing<TS>(double sinThetaO, double phiO, double h, TS absorption)
        where TS : struct, ISpectrum<TS>
    {
        double cosThetaO = Math.Sqrt(Math.Max(0, 1 - sinThetaO * sinThetaO));
        (double gammaT, TS through) = Inside(sinThetaO, cosThetaO, h, absorption);
        (TS mirrored, TS passed, TS mirroredInside, TS rest) = Attenuations(cosThetaO, h, through);

        return new Seen<TS>(
            sinThetaO, cosThetaO, phiO, Math.Asin(Math.Clamp(h, -1, 1)), gammaT, mirrored, passed,
            mirroredInside, rest, absorption);
    }

    /// <summary>
    /// This method returns how much of the light arriving from one direction a strand sends toward the
    /// eye, per unit of solid angle, for light of unit radiance.
    /// <para>
    /// It is PBRT's scattering function times the cosine its integrator multiplies by -- the cosine is
    /// part of the hair's lobes, which PBRT divides out again -- so the light leaving is this times the
    /// light arriving, summed over directions, and integrated over every direction it comes to how much
    /// of the light the strand does not absorb.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="seen">What the scattering owes to the eye's direction.</param>
    /// <param name="sinThetaI">The sine of the light's angle out of the plane square to the strand.</param>
    /// <param name="phiI">The light's angle around the strand.</param>
    /// <returns>The share of the light sent toward the eye, per band.</returns>
    public TS Scattered<TS>(in Seen<TS> seen, double sinThetaI, double phiI)
        where TS : struct, ISpectrum<TS>
    {
        double cosThetaI = Math.Sqrt(Math.Max(0, 1 - sinThetaI * sinThetaI));
        double phi = phiI - seen.PhiO;
        TS sum = TS.Black;

        for (int lobe = 0; lobe < Lobes; lobe++)
        {
            (double sinThetaOp, double cosThetaOp) = Tilted(seen, lobe);
            double along = Longitudinal(cosThetaI, Math.Abs(cosThetaOp), sinThetaI, sinThetaOp, _variance[lobe]);
            double around = Azimuthal(phi, lobe, seen.GammaO, seen.GammaT);
            TS attenuation = lobe switch { 0 => seen.Mirrored, 1 => seen.Through, _ => seen.MirroredInside };

            sum += attenuation * (along * around);
        }

        double rest = Longitudinal(cosThetaI, seen.CosThetaO, sinThetaI, seen.SinThetaO, _variance[Lobes]);

        return sum + seen.Rest * (rest / (2 * Math.PI));
    }

    /// <summary>
    /// This method returns how much a strand's inside must absorb, over its radius, for hair to come out
    /// the given color: Chiang et al.'s fit, for one channel.
    /// <para>
    /// **The color is what a head of such hair looks like, not one strand**: the fit was made against
    /// hair rendered with every bounce between strands, so it is the color of light hair once light has
    /// found its way through it.
    /// </para>
    /// </summary>
    /// <param name="color">The channel of the color to match, from nought to one.</param>
    /// <param name="roughness">How rough the strand is around itself.</param>
    /// <returns>How much the inside absorbs in that channel.</returns>
    public static double AbsorptionFor(double color, double roughness)
    {
        double share = Math.Log(Math.Clamp(color, 1e-6, 1)) / MatchingScale(roughness);

        return share * share;
    }

    /// <summary>
    /// This method returns the color a head of hair whose strands absorb as given comes out: the fit
    /// <see cref="AbsorptionFor(double, double)"/> makes, turned round.
    /// </summary>
    /// <param name="absorption">How much the inside absorbs, over its radius, in one channel.</param>
    /// <param name="roughness">How rough the strand is around itself.</param>
    /// <returns>The channel of the color.</returns>
    public static double ColorFor(double absorption, double roughness)
    {
        return Math.Exp(-Math.Sqrt(Math.Max(0, absorption)) * MatchingScale(roughness));
    }

    /// <summary>
    /// This method returns how much a strand's inside absorbs, over its radius, for hair to come out the
    /// given color.
    /// </summary>
    /// <param name="color">The color to match.</param>
    /// <param name="roughness">How rough the strand is around itself.</param>
    /// <returns>How much the inside absorbs, in red, green and blue.</returns>
    public static Color AbsorptionFor(Color color, double roughness)
    {
        return new Color(
            AbsorptionFor(color.Red, roughness), AbsorptionFor(color.Green, roughness),
            AbsorptionFor(color.Blue, roughness));
    }

    /// <summary>
    /// This method returns the color a head of hair whose strands absorb as given comes out.
    /// </summary>
    /// <param name="absorption">How much the inside absorbs, in red, green and blue.</param>
    /// <param name="roughness">How rough the strand is around itself.</param>
    /// <returns>The color.</returns>
    public static Color ColorFor(Color absorption, double roughness)
    {
        return new Color(
            ColorFor(absorption.Red, roughness), ColorFor(absorption.Green, roughness),
            ColorFor(absorption.Blue, roughness));
    }

    /// <summary>
    /// This method returns how much a strand's inside absorbs, over its radius, for the given amounts of
    /// the two melanins: eumelanin, which is brown to black, and pheomelanin, which is red.
    /// </summary>
    /// <param name="melanin">How much of each.</param>
    /// <returns>How much the inside absorbs, in red, green and blue.</returns>
    public static Color AbsorptionFor(Melanin melanin)
    {
        return EumelaninAbsorption * melanin.Eumelanin + PheomelaninAbsorption * melanin.Pheomelanin;
    }

    /// <summary>
    /// This method works out the way through a strand's inside: the angle around the strand at which the
    /// light refracted in, and how much of each band survives the trip across.
    /// </summary>
    private static (double GammaT, TS Through) Inside<TS>(double sinTheta, double cosTheta, double h, TS absorption)
        where TS : struct, ISpectrum<TS>
    {
        double sinThetaT = sinTheta / Index;
        double cosThetaT = Math.Sqrt(Math.Max(0, 1 - sinThetaT * sinThetaT));

        // The index the light sees in the plane square to the strand, which is higher the more steeply
        // it crosses: Bravais's.
        double indexAround = Math.Sqrt(Math.Max(0, Index * Index - sinTheta * sinTheta)) / Math.Max(cosTheta, 1e-12);
        double sinGammaT = Math.Clamp(h / indexAround, -1, 1);
        double cosGammaT = Math.Sqrt(1 - sinGammaT * sinGammaT);
        double length = 2 * cosGammaT / Math.Max(cosThetaT, 1e-12);
        TS through = TS.White;

        for (int band = 0; band < TS.Count; band++)
            through[band] = Math.Exp(-absorption[band] * length);

        return (Math.Asin(sinGammaT), through);
    }

    /// <summary>
    /// This method returns the eye's angle out of the plane square to the strand, shifted for one lobe by
    /// the tilt of the strand's scales: the mirror off the outside by twice it one way, the trip through by
    /// once the other, the mirror inside by four times.
    /// </summary>
    private (double Sin, double Cos) Tilted<TS>(in Seen<TS> seen, int lobe)
        where TS : struct, ISpectrum<TS>
    {
        return lobe switch
        {
            0 => (seen.SinThetaO * _cosTilt[1] - seen.CosThetaO * _sinTilt[1],
                seen.CosThetaO * _cosTilt[1] + seen.SinThetaO * _sinTilt[1]),
            1 => (seen.SinThetaO * _cosTilt[0] + seen.CosThetaO * _sinTilt[0],
                seen.CosThetaO * _cosTilt[0] - seen.SinThetaO * _sinTilt[0]),
            _ => (seen.SinThetaO * _cosTilt[2] + seen.CosThetaO * _sinTilt[2],
                seen.CosThetaO * _cosTilt[2] - seen.SinThetaO * _sinTilt[2])
        };
    }

    /// <summary>
    /// This method returns the direction one lobe draws its light from most: along the strand, the mirror
    /// of the eye's angle shifted by the scales' tilt; round it, where light through a circle at this
    /// offset leaves it after that many trips across.  The last lobe draws from every way round alike, and
    /// is given the mirror of the eye's angle and the eye's own way round.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="seen">What the scattering owes to the eye's direction.</param>
    /// <param name="lobe">Which lobe, from nought to three.</param>
    /// <returns>The sine of the direction's angle out of the plane square to the strand, and its angle
    /// round it.</returns>
    public (double SinThetaI, double PhiI) CenterOf<TS>(in Seen<TS> seen, int lobe)
        where TS : struct, ISpectrum<TS>
    {
        double thetaO = Math.Asin(Math.Clamp(seen.SinThetaO, -1, 1));
        double shift = lobe switch { 0 => 2 * Tilt, 1 => -Tilt, 2 => -4 * Tilt, _ => 0 };
        double turn = lobe < Lobes ? 2 * lobe * seen.GammaT - 2 * seen.GammaO + lobe * Math.PI : 0;

        return (Math.Sin(shift - thetaO), seen.PhiO + turn);
    }

    /// <summary>
    /// This method returns how much of the light one lobe carries, per band: what is mirrored off the
    /// outside, what passes through, what is mirrored inside, and the rest.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="seen">What the scattering owes to the eye's direction.</param>
    /// <param name="lobe">Which lobe, from nought to three.</param>
    /// <returns>The share it carries.</returns>
    public static TS Carried<TS>(in Seen<TS> seen, int lobe)
        where TS : struct, ISpectrum<TS>
    {
        return lobe switch { 0 => seen.Mirrored, 1 => seen.Through, 2 => seen.MirroredInside, _ => seen.Rest };
    }

    /// <summary>
    /// This method works out how much of the light ends up in each lobe: mirrored off the outside,
    /// passed through, mirrored once inside, and everything after.
    /// </summary>
    private static (TS Mirrored, TS Through, TS MirroredInside, TS Remaining) Attenuations<TS>(
        double cosThetaO, double h, TS through)
        where TS : struct, ISpectrum<TS>
    {
        double fresnel = Reflectance(cosThetaO * Math.Sqrt(Math.Max(0, 1 - h * h)));
        TS mirrored = TS.White * fresnel;
        TS passed = through * ((1 - fresnel) * (1 - fresnel));
        TS mirroredInside = passed * through * fresnel;
        TS rest = TS.Black;

        // What is left after the mirror inside goes on bouncing, losing as much each time: a geometric
        // series, summed band by band.
        for (int band = 0; band < TS.Count; band++)
        {
            double each = through[band] * fresnel;

            rest[band] = each < 1 ? mirroredInside[band] * each / (1 - each) : 0;
        }

        return (mirrored, passed, mirroredInside, rest);
    }

    /// <summary>
    /// This method returns how much of the light meeting a dielectric at the given cosine, from outside,
    /// it mirrors: Fresnel's, for unpolarized light.
    /// </summary>
    private static double Reflectance(double cosine)
    {
        cosine = Math.Clamp(cosine, 0, 1);

        double sinT = Math.Sqrt(Math.Max(0, 1 - cosine * cosine)) / Index;

        if (sinT >= 1)
            return 1;

        double cosT = Math.Sqrt(Math.Max(0, 1 - sinT * sinT));
        double parallel = (Index * cosine - cosT) / (Index * cosine + cosT);
        double perpendicular = (cosine - Index * cosT) / (cosine + Index * cosT);

        return (parallel * parallel + perpendicular * perpendicular) / 2;
    }

    /// <summary>
    /// This method returns the lobe along the strand: d'Eon et al.'s, which keeps its energy however
    /// rough the strand, worked out in logarithms when it is narrow so that it does not overflow.
    /// </summary>
    private static double Longitudinal(
        double cosThetaI, double cosThetaO, double sinThetaI, double sinThetaO, double variance)
    {
        double a = cosThetaI * cosThetaO / variance;
        double b = sinThetaI * sinThetaO / variance;

        return variance <= 0.1
            ? Math.Exp(LogBessel(a) - b - 1 / variance + 0.6931 + Math.Log(1 / (2 * variance)))
            : Math.Exp(-b) * Bessel(a) / (Math.Sinh(1 / variance) * 2 * variance);
    }

    /// <summary>
    /// This method returns the lobe around the strand for one way of leaving it: a logistic about the
    /// way a ray through a circle at this offset leaves it, cut off at half a turn either way.
    /// </summary>
    private double Azimuthal(double phi, int lobe, double gammaO, double gammaT)
    {
        double away = phi - (2 * lobe * gammaT - 2 * gammaO + lobe * Math.PI);

        away = Math.IEEERemainder(away, 2 * Math.PI);

        return Logistic(away, _spread) / (LogisticCdf(Math.PI, _spread) - LogisticCdf(-Math.PI, _spread));
    }

    private static double Logistic(double x, double scale)
    {
        x = Math.Abs(x);

        double e = Math.Exp(-x / scale);

        return e / (scale * (1 + e) * (1 + e));
    }

    private static double LogisticCdf(double x, double scale) => 1 / (1 + Math.Exp(-x / scale));

    /// <summary>
    /// This method returns the modified Bessel function of the first kind and order nought, by its
    /// series.
    /// </summary>
    private static double Bessel(double x)
    {
        double value = 0;
        double power = 1;
        double factorial = 1;
        double fours = 1;

        for (int index = 0; index < 10; index++)
        {
            if (index > 1)
                factorial *= index;

            value += power / (fours * factorial * factorial);
            power *= x * x;
            fours *= 4;
        }

        return value;
    }

    /// <summary>
    /// This method returns the logarithm of <see cref="Bessel"/>, by its asymptote where the series
    /// would overflow.
    /// </summary>
    private static double LogBessel(double x)
    {
        return x > 12
            ? x + 0.5 * (-Math.Log(2 * Math.PI) + Math.Log(1 / x) + 1 / (8 * x))
            : Math.Log(Bessel(x));
    }

    /// <summary>
    /// This method returns the scale in Chiang et al.'s fit between absorption and color, for the given
    /// roughness around the strand.
    /// </summary>
    private static double MatchingScale(double roughness)
    {
        double b = roughness;

        return 5.969 - 0.215 * b + 2.532 * b * b - 10.73 * b * b * b + 5.574 * b * b * b * b +
               0.245 * b * b * b * b * b;
    }
}

/// <summary>
/// This struct holds how much of each of the two melanins a hair holds, which is what colors natural
/// hair: eumelanin, brown to black, and pheomelanin, red.
/// </summary>
/// <param name="Eumelanin">How much eumelanin.</param>
/// <param name="Pheomelanin">How much pheomelanin.</param>
public readonly record struct Melanin(double Eumelanin, double Pheomelanin)
{
    /// <summary>
    /// This holds the natural shades of hair by name.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Melanin> Shades = new Dictionary<string, Melanin>
    {
        ["black"] = new (8, 0),
        ["brown"] = new (1.3, 0),
        ["auburn"] = new (0.5, 0.8),
        ["ginger"] = new (0.03, 0.6),
        ["blond"] = new (0.1, 0.05),
        ["platinum"] = new (0.01, 0.005)
    };
}
