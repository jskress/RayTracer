using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class holds what the air does to light passing through it.
/// <para>
/// Two quite different things happen, and keeping them apart is the whole of understanding why a sky
/// looks the way it does.  Air molecules are far smaller than the light they meet, and scatter it in
/// proportion to the inverse fourth power of its wavelength -- so blue light is turned aside some ten
/// times more readily than red.  That is Rayleigh scattering, and it is why the sky is blue overhead
/// and why the sun reddens as it sets: looking away from the sun you see the light that was turned
/// aside, and looking at it you see what was left after the blue had been taken out.
/// </para>
/// <para>
/// Haze, dust and water droplets are a different matter.  They are comparable in size to the light or
/// larger, so they turn nearly all wavelengths alike -- which is why haze is white or gray rather
/// than colored -- and they turn it strongly forward, which is why the sky whitens in a bright ring
/// around the sun.  That is Mie scattering.
/// </para>
/// <para>
/// Both thin out with height, and not at the same rate: the air itself falls away with a scale height
/// of some eight and a half kilometres, while haze is mostly held in the lowest kilometre or two.
/// That difference is why a hazy day washes out the horizon while leaving the zenith nearly as blue
/// as ever -- looking level you are looking through nearly all of the haze, and looking up you are
/// looking through almost none of it.
/// </para>
/// </summary>
public class Atmosphere
{
    /// <summary>
    /// This field holds the radius of the planet, in metres.
    /// </summary>
    public const double GroundRadius = 6_360_000;

    /// <summary>
    /// This field holds the radius at which the air is taken to have run out, in metres.  There is no
    /// true edge, the air simply thinning forever, but by sixty kilometres up there is far too little
    /// left to turn any light worth counting.
    /// </summary>
    public const double TopRadius = 6_420_000;

    /// <summary>
    /// This field holds how quickly the air thins with height, in metres.
    /// <para>
    /// It is <c>kT/mg</c> for air at fifteen degrees, which comes to 8435 -- not the 8000 that gets
    /// passed around, which is the same quantity worked out at freezing.  The difference is worth
    /// having: an atmosphere thinning at this rate holds exactly as much air in a column above a point
    /// as the real one does, given the density at the ground, and that is what makes the depth of it
    /// come out right rather than five percent heavy.
    /// </para>
    /// </summary>
    public const double AirScaleHeight = 8435;

    /// <summary>
    /// This field holds how quickly haze thins with height, in metres.  Far faster than the air,
    /// haze being a thing of the weather rather than of the atmosphere at large.
    /// </summary>
    public const double HazeScaleHeight = 1200;

    /// <summary>
    /// This field holds how many air molecules there are in a cubic metre at sea level, at fifteen
    /// degrees and one atmosphere.
    /// </summary>
    private const double MoleculesPerCubicMetre = 2.5469e25;

    /// <summary>
    /// This field holds how much air molecules depart from being perfect spheres, which lets a little
    /// more light through sideways than a round scatterer would.  It raises the scattering by about
    /// five percent, and leaving it out is a common way to end up with a sky that is slightly too
    /// dim.
    /// </summary>
    private const double Depolarization = 0.0279;

    /// <summary>
    /// This property holds how hazy the air is.  One is air with nothing in it at all, which happens
    /// nowhere; two to three is a clear day; six and beyond is thick haze, with the horizon lost in
    /// white long before anything else has gone.
    /// </summary>
    public double Turbidity { get; set; } = 2.5;

    /// <summary>
    /// This property holds the light that has been turned aside more than once, or <c>null</c> to
    /// follow only the first turn.
    /// <para>
    /// It is left out while that very table is being worked out, since it is built from the
    /// once-turned light and cannot be built from itself.
    /// </para>
    /// </summary>
    public MultipleScattering Bounced { get; set; }

    /// <summary>
    /// This method returns how high the sun stands in the sky as seen from a particular place, which
    /// is not the same everywhere: a view toward the horizon crosses hundreds of miles, and the sun
    /// is lower at the far end of that than it is overhead.
    /// </summary>
    /// <param name="where">The place in question.</param>
    /// <param name="towardSun">Which way the sun lies.</param>
    /// <returns>The cosine of the sun's angle from straight up there.</returns>
    private static double TowardTheSunAt(Point where, Vector towardSun)
    {
        double length = Math.Sqrt(where.X * where.X + where.Y * where.Y + where.Z * where.Z);

        if (length < 1e-9)
            return towardSun.Y;

        return (where.X * towardSun.X + where.Y * towardSun.Y + where.Z * towardSun.Z) / length;
    }

    /// <summary>
    /// This method returns how much of a beam sent out along a ray is turned aside and sent back,
    /// band by band -- what a unit of light given off evenly at a place would get back from the air
    /// around it.
    /// </summary>
    /// <param name="view">Which way to look.</param>
    /// <param name="height">How high the place is, in metres.</param>
    /// <returns>How much comes back, band by band.</returns>
    public double[] TurnedBackAlong(Vector view, double height)
    {
        double[] back = new double[SpectralColor.Bands];
        Point from = new (0, GroundRadius + Math.Max(0, height), 0);
        double crossing = DistanceThroughAir(from, view);

        if (crossing <= 0)
            return back;

        double[] airScattering = AirScatteringPerBand();
        double hazeScattering = HazeScattering();
        double hazeExtinction = HazeExtinction();
        double step = crossing / Steps;
        double airBehind = 0, hazeBehind = 0;

        for (int place = 0; place < Steps; place++)
        {
            double aboveGround = HeightOf(from + view * ((place + 0.5) * step));
            double air = AirDensityAt(aboveGround) * step;
            double haze = HazeDensityAt(aboveGround) * step;

            airBehind += air;
            hazeBehind += haze;

            for (int band = 0; band < SpectralColor.Bands; band++)
            {
                double gone = airScattering[band] * airBehind + hazeExtinction * hazeBehind;

                back[band] += Math.Exp(-gone) * (airScattering[band] * air + hazeScattering * haze);
            }
        }

        return back;
    }

    /// <summary>
    /// This method returns how strongly the air itself turns light of the given wavelength aside, per
    /// metre, at sea level.
    /// <para>
    /// This is Rayleigh's own result and nothing is fitted in it: how much a scatterer far smaller
    /// than a wavelength turns light follows from how strongly the light polarizes it, which is what
    /// the refractive index measures.  The inverse fourth power of the wavelength falls out of the
    /// arithmetic rather than being put in.
    /// </para>
    /// </summary>
    /// <param name="wavelength">The wavelength in question, in nanometers.</param>
    /// <returns>How much is turned aside per metre at sea level.</returns>
    public static double AirScatteringAt(double wavelength)
    {
        double metres = wavelength * 1e-9;
        double refractive = RefractiveIndexOfAir(wavelength);
        double polarizability = refractive * refractive - 1;
        double king = (6 + 3 * Depolarization) / (6 - 7 * Depolarization);

        return 8 * Math.PI * Math.PI * Math.PI * polarizability * polarizability /
               (3 * MoleculesPerCubicMetre * Math.Pow(metres, 4)) * king;
    }

    /// <summary>
    /// This method returns how strongly haze turns light aside, per metre, at sea level.
    /// <para>
    /// Where the air's own scattering is derived, this is measured and approximate, and deliberately
    /// so: what haze is made of varies from day to day and place to place far more than any formula
    /// could follow.  What can be said is that droplets and dust are large enough to turn every
    /// wavelength much alike, so unlike the air this barely depends on color at all.
    /// </para>
    /// </summary>
    /// <returns>How much is turned aside per metre at sea level.</returns>
    public double HazeScattering()
    {
        // Air with a turbidity of one has no haze in it by definition, so what haze does must run
        // from nothing at one rather than from nothing at nothing.
        return 21e-6 * Math.Max(0, Turbidity - 1) / 1.5;
    }

    /// <summary>
    /// This method returns how much haze swallows light outright rather than turning it aside.  Real
    /// droplets absorb a little of what strikes them, so rather less than a tenth of what haze takes
    /// out of a beam never goes anywhere at all.
    /// </summary>
    /// <returns>How much is taken out per metre at sea level, scattered and swallowed together.</returns>
    public double HazeExtinction()
    {
        return HazeScattering() / 0.9;
    }

    /// <summary>
    /// This method returns how much of the sea level air remains at the given height.
    /// </summary>
    /// <param name="height">How far above the ground, in metres.</param>
    /// <returns>The share of the sea level amount that is left there.</returns>
    public static double AirDensityAt(double height)
    {
        return Math.Exp(-Math.Max(0, height) / AirScaleHeight);
    }

    /// <summary>
    /// This method returns how much of the sea level haze remains at the given height.
    /// </summary>
    /// <param name="height">How far above the ground, in metres.</param>
    /// <returns>The share of the sea level amount that is left there.</returns>
    public static double HazeDensityAt(double height)
    {
        return Math.Exp(-Math.Max(0, height) / HazeScaleHeight);
    }

    /// <summary>
    /// This method returns how much air stands between the ground and space, straight up, measured as
    /// how much light of the given wavelength it takes out.
    /// <para>
    /// This is the number the whole model is held to, because it has been measured: for green light
    /// at 550 nanometers, looking straight up through the whole atmosphere, the answer is 0.0973.
    /// Nothing here was fitted to that -- it falls out of the refractive index of air, how much of it
    /// there is, and how quickly it thins -- so agreeing with it is a real check rather than a
    /// tautology.
    /// </para>
    /// </summary>
    /// <param name="wavelength">The wavelength in question, in nanometers.</param>
    /// <returns>How much of that light a vertical column of air takes out.</returns>
    public static double VerticalAirDepthAt(double wavelength)
    {
        // A column of something thinning exponentially holds exactly as much as a column of the
        // sea level amount one scale height deep, which is why no integral is needed here.
        return AirScatteringAt(wavelength) * AirScaleHeight;
    }

    /// <summary>
    /// This method returns the refractive index of air at the given wavelength.
    /// <para>
    /// Air bends blue light very slightly more than red, and that difference -- in the fourth decimal
    /// place -- is squared and then divided by the fourth power of the wavelength on its way into the
    /// scattering, so it matters more than its size suggests.  The formula is Edlén's, fitted to
    /// measurements of standard air at fifteen degrees and one atmosphere.
    /// </para>
    /// </summary>
    /// <param name="wavelength">The wavelength in question, in nanometers.</param>
    /// <returns>How much more slowly light of that wavelength travels through air than through
    /// nothing.</returns>
    public static double RefractiveIndexOfAir(double wavelength)
    {
        // Edlén works in waves per micrometer rather than in wavelength.
        double waves = 1000 / wavelength;
        double squared = waves * waves;

        return 1 + (8060.51 +
            2480990 / (132.274 - squared) +
            17455.7 / (39.32957 - squared)) * 1e-8;
    }

    /// <summary>
    /// This method returns how much light the air turns aside at each wavelength the renderer works
    /// in, per metre at sea level.
    /// </summary>
    /// <returns>The scattering, band by band.</returns>
    public static double[] AirScatteringPerBand()
    {
        double[] scattering = new double[SpectralColor.Bands];

        for (int band = 0; band < SpectralColor.Bands; band++)
            scattering[band] = AirScatteringAt(SpectralColor.WavelengthOf(band));

        return scattering;
    }

    /// <summary>
    /// This property holds how strongly haze prefers to carry light on the way it was already going.
    /// Droplets are large compared with the light, and large scatterers throw it forward hard: this is
    /// why there is a bright ring around the sun on a hazy day, and why looking away from the sun the
    /// haze contributes rather little.
    /// </summary>
    public double HazeForwardness { get; set; } = 0.76;

    /// <summary>
    /// This property holds how many places a view is sampled in on its way out through the air.
    /// </summary>
    public int Steps { get; set; } = 64;

    /// <summary>
    /// This property holds how many places the path back to the sun is sampled in.  Fewer are needed
    /// than for the view itself: what is wanted from it is only how much air stands in the way, which
    /// is a far smoother thing than what the view is accumulating.
    /// </summary>
    public int StepsTowardSun { get; set; } = 16;

    /// <summary>
    /// This method returns how much light of each wavelength arrives from the given direction.
    /// <para>
    /// This is the radiative transfer equation for the air, and nothing more: walk out along the
    /// view, and at every place along it ask how much sunlight still reaches that place, how much of
    /// it is turned toward the eye there, and how much of <i>that</i> survives the rest of the way
    /// back.  Everything a clear sky does follows from those three -- the blue overhead, the pale
    /// horizon, the ring of glare around the sun, the red at the end of the day.
    /// </para>
    /// <para>
    /// Light turned more than once is not followed.  That is a real omission rather than a rounding:
    /// it leaves the sky too dark low down, where a view skims through the most air and so has the
    /// most chances to be turned again, and it means there is no twilight to speak of once the sun is
    /// down.  Overhead, and with the sun up, almost all of the light has been turned exactly once and
    /// the answer is very nearly the whole of it.
    /// </para>
    /// </summary>
    /// <param name="view">Which way is being looked, as a unit vector with Y upward.</param>
    /// <param name="towardSun">Which way the sun lies, as a unit vector with Y upward.</param>
    /// <param name="height">How far the viewer stands above the ground, in metres.</param>
    /// <returns>How much light arrives, band by band.</returns>
    public double[] RadianceToward(Vector view, Vector towardSun, double height)
    {
        double[] arriving = new double[SpectralColor.Bands];
        Point eye = new (0, GroundRadius + Math.Max(0, height), 0);
        double crossing = DistanceThroughAir(eye, view);

        if (crossing <= 0)
            return arriving;

        double[] airScattering = AirScatteringPerBand();
        double[] sunlight = SunlightPerBand();
        double hazeScattering = HazeScattering();
        double hazeExtinction = HazeExtinction();
        double cosine = view.Dot(towardSun);
        double airTurn = 3 * (1 + cosine * cosine) / (16 * Math.PI);
        double hazeTurn = HenyeyGreenstein(cosine, HazeForwardness);
        double step = crossing / Steps;

        // How much air and haze the view has already passed through.  These are kept as plain amounts
        // rather than as transmittances because they do not depend on the wavelength -- only what they
        // are multiplied by does -- so one walk serves every band at once.
        double airBehind = 0, hazeBehind = 0;

        for (int place = 0; place < Steps; place++)
        {
            Point where = eye + view * ((place + 0.5) * step);
            double aboveGround = HeightOf(where);
            double air = AirDensityAt(aboveGround) * step;
            double haze = HazeDensityAt(aboveGround) * step;

            airBehind += air;
            hazeBehind += haze;

            (double airToSun, double hazeToSun, bool inShadow) = TowardTheSunFrom(where, towardSun);

            // A place the sun cannot see gets nothing directly, but light that has been turned aside
            // more than once still finds it -- which is the whole of why a sky stays lit after sunset.
            if (inShadow)
            {
                airToSun = double.PositiveInfinity;
                hazeToSun = 0;
            }

            double[] alsoArriving = Bounced?.At(aboveGround, TowardTheSunAt(where, towardSun));

            for (int band = 0; band < SpectralColor.Bands; band++)
            {
                double toEye = airScattering[band] * airBehind + hazeExtinction * hazeBehind;
                double survives = Math.Exp(-toEye);
                double turnedHere = airScattering[band] * air * airTurn + hazeScattering * haze * hazeTurn;

                arriving[band] += survives * Math.Exp(-(airScattering[band] * airToSun +
                    hazeExtinction * hazeToSun)) * sunlight[band] * turnedHere;

                // Light that had already been turned aside somewhere else before it was turned again
                // here.  It arrives from every direction rather than from the sun, so it takes no
                // phase function: an even spread scattered by any shape at all is still an even
                // spread.
                if (alsoArriving is not null)
                {
                    arriving[band] += survives * alsoArriving[band] *
                        (airScattering[band] * air + hazeScattering * haze);
                }
            }
        }

        return arriving;
    }

    /// <summary>
    /// This method returns how much sunlight of each wavelength survives the trip down to a viewer,
    /// which is what makes a low sun red.
    /// </summary>
    /// <param name="towardSun">Which way the sun lies, as a unit vector with Y upward.</param>
    /// <param name="height">How far the viewer stands above the ground, in metres.</param>
    /// <returns>What is left of the sunlight, band by band.</returns>
    public double[] SunlightAfterAir(Vector towardSun, double height)
    {
        double[] arriving = new double[SpectralColor.Bands];
        Point eye = new (0, GroundRadius + Math.Max(0, height), 0);
        (double air, double haze, bool below) = TowardTheSunFrom(eye, towardSun);

        if (below)
            return arriving;

        double[] airScattering = AirScatteringPerBand();
        double[] sunlight = SunlightPerBand();
        double hazeExtinction = HazeExtinction();

        for (int band = 0; band < SpectralColor.Bands; band++)
        {
            arriving[band] = sunlight[band] *
                Math.Exp(-(airScattering[band] * air + hazeExtinction * haze));
        }

        return arriving;
    }

    /// <summary>
    /// This method works out how much air and haze lies between a place and the sun, and whether the
    /// planet itself is in the way.
    /// </summary>
    /// <param name="from">Where to start.</param>
    /// <param name="towardSun">Which way the sun lies.</param>
    /// <returns>How much air and haze stand in the way, and whether the sun is below the horizon
    /// from there.</returns>
    private (double Air, double Haze, bool InShadow) TowardTheSunFrom(Point from, Vector towardSun)
    {
        if (HitsTheGround(from, towardSun))
            return (0, 0, true);

        double crossing = DistanceThroughAir(from, towardSun);

        if (crossing <= 0)
            return (0, 0, false);

        double step = crossing / StepsTowardSun;
        double air = 0, haze = 0;

        for (int place = 0; place < StepsTowardSun; place++)
        {
            double aboveGround = HeightOf(from + towardSun * ((place + 0.5) * step));

            air += AirDensityAt(aboveGround) * step;
            haze += HazeDensityAt(aboveGround) * step;
        }

        return (air, haze, false);
    }

    /// <summary>
    /// This method returns how far a ray travels before it leaves the air, either by reaching the top
    /// of it or by running into the ground.
    /// </summary>
    /// <param name="from">Where the ray starts.</param>
    /// <param name="heading">Which way it goes.</param>
    /// <returns>How far it gets.</returns>
    private static double DistanceThroughAir(Point from, Vector heading)
    {
        return HitsTheGround(from, heading)
            ? ReachesTheGroundAt(from, heading)
            : FarSideOfSphere(from, heading, TopRadius);
    }

    /// <summary>
    /// This method reports whether a ray runs into the planet.
    /// <para>
    /// It is asked as "does the ray pass within the planet's radius, and does it do so ahead rather
    /// than behind" rather than by looking for a crossing, and that is deliberate.  Every interesting
    /// case here starts <i>on</i> the ground or very near it, and a ray starting exactly on a sphere
    /// meets it at no distance at all -- so looking for a crossing at a positive distance says that a
    /// ray pointing straight down misses the planet, and lets it march twelve thousand kilometres
    /// through solid rock.  Asking about the closest approach has no such corner: pointing down, the
    /// nearest approach is ahead and inside; pointing up, it is behind.
    /// </para>
    /// </summary>
    /// <param name="from">Where the ray starts.</param>
    /// <param name="heading">Which way it goes.</param>
    /// <returns><c>true</c>, if the ground is in the way.</returns>
    private static bool HitsTheGround(Point from, Vector heading)
    {
        double alongToClosest = -(from.X * heading.X + from.Y * heading.Y + from.Z * heading.Z);

        if (alongToClosest <= 0)
            return false;

        double squaredFromCenter = from.X * from.X + from.Y * from.Y + from.Z * from.Z;
        double squaredClosest = squaredFromCenter - alongToClosest * alongToClosest;

        return squaredClosest < GroundRadius * GroundRadius;
    }

    /// <summary>
    /// This method returns how far above the ground a place is.
    /// </summary>
    /// <param name="where">The place in question.</param>
    /// <returns>Its height above the ground, in metres.</returns>
    private static double HeightOf(Point where)
    {
        return Math.Sqrt(where.X * where.X + where.Y * where.Y + where.Z * where.Z) - GroundRadius;
    }

    /// <summary>
    /// This method returns where a ray from inside a sphere leaves it.
    /// </summary>
    /// <param name="from">Where the ray starts.</param>
    /// <param name="heading">Which way it goes.</param>
    /// <param name="radius">The radius of the sphere.</param>
    /// <returns>How far the ray goes before leaving, or nothing if it never does.</returns>
    private static double FarSideOfSphere(Point from, Vector heading, double radius)
    {
        double alongToClosest = -(from.X * heading.X + from.Y * heading.Y + from.Z * heading.Z);
        double squaredFromCenter =
            from.X * from.X + from.Y * from.Y + from.Z * from.Z;
        double halfChordSquared =
            alongToClosest * alongToClosest - squaredFromCenter + radius * radius;

        return halfChordSquared < 0 ? 0 : alongToClosest + Math.Sqrt(halfChordSquared);
    }

    /// <summary>
    /// This method returns how far a ray gets before it meets the ground, for a ray already known to
    /// meet it.
    /// </summary>
    /// <param name="from">Where the ray starts.</param>
    /// <param name="heading">Which way it goes.</param>
    /// <returns>How far it gets, which is nothing for a ray starting on the ground and pointing
    /// into it.</returns>
    private static double ReachesTheGroundAt(Point from, Vector heading)
    {
        double alongToClosest = -(from.X * heading.X + from.Y * heading.Y + from.Z * heading.Z);
        double squaredFromCenter =
            from.X * from.X + from.Y * from.Y + from.Z * from.Z;
        double halfChordSquared =
            alongToClosest * alongToClosest - squaredFromCenter + GroundRadius * GroundRadius;

        return halfChordSquared <= 0 ? 0 : Math.Max(0, alongToClosest - Math.Sqrt(halfChordSquared));
    }

    /// <summary>
    /// This method gives how strongly a large scatterer carries light on the way it was already
    /// going.
    /// <para>
    /// Note that this and the Rayleigh turn beside it are written as shares of a whole sphere -- they
    /// come to one when summed over every direction -- and not in the form
    /// <see cref="Medium"/> uses, where an even spread is one.  The two differ by a factor of four pi.
    /// The form here is the one the transfer equation is written in, and this is writing that equation
    /// out rather than handing a value to the renderer's own sampler.
    /// </para>
    /// </summary>
    /// <param name="cosine">The cosine of the angle between where the light was going and where it
    /// goes now.</param>
    /// <param name="forwardness">How strongly it is thrown forward.</param>
    /// <returns>The share of the light that goes that way.</returns>
    private static double HenyeyGreenstein(double cosine, double forwardness)
    {
        double squared = forwardness * forwardness;
        double denominator = 1 + squared - 2 * forwardness * cosine;

        return (1 - squared) / (4 * Math.PI * denominator * Math.Sqrt(denominator));
    }

    /// <summary>
    /// This method returns how much sunlight of each wavelength arrives at the top of the air.
    /// <para>
    /// The sun is taken as a body glowing at 5778 kelvin, which is what its surface comes to.  The
    /// true spectrum departs from that by a few percent here and there, most of it where the sun's
    /// own outer gases absorb their particular colors on the way out, and the shape of those is a
    /// table rather than anything derivable.  It matters much less than it sounds: what makes a
    /// sunset red is overwhelmingly what the air does on the way in, not the few percent the sun
    /// left with.
    /// </para>
    /// <para>
    /// It is scaled so that the light arriving at the top of the air is of strength one, which puts a
    /// sky on the same footing as every other light in a scene: what reaches the ground is then less
    /// than one by however much the air took, which is as it should be.
    /// </para>
    /// </summary>
    /// <returns>The sunlight above the air, band by band.</returns>
    public static double[] SunlightPerBand()
    {
        return _sunlight ??= WorkOutSunlight();
    }

    private static double[] _sunlight;

    /// <summary>
    /// This method works out the sunlight above the air, once.
    /// </summary>
    /// <returns>The sunlight above the air, band by band.</returns>
    private static double[] WorkOutSunlight()
    {
        double[] sunlight = new double[SpectralColor.Bands];

        for (int band = 0; band < SpectralColor.Bands; band++)
            sunlight[band] = Glow(SpectralColor.WavelengthOf(band), 5778);

        // Scaled so the whole of it stands at a strength of one.
        Color asSeen = SpectralColor.ToColor(sunlight);
        double brightness =
            0.2126 * asSeen.Red + 0.7152 * asSeen.Green + 0.0722 * asSeen.Blue;

        for (int band = 0; band < SpectralColor.Bands; band++)
            sunlight[band] /= brightness;

        return sunlight;
    }

    /// <summary>
    /// This method gives how brightly something at a given temperature glows at a given wavelength.
    /// </summary>
    /// <param name="wavelength">The wavelength in question, in nanometers.</param>
    /// <param name="temperature">How hot the thing is, in kelvin.</param>
    /// <returns>How brightly it glows there.</returns>
    private static double Glow(double wavelength, double temperature)
    {
        const double PlanckConstant = 6.62607015e-34;
        const double SpeedOfLight = 2.99792458e8;
        const double BoltzmannConstant = 1.380649e-23;

        double metres = wavelength * 1e-9;
        double front = 2 * PlanckConstant * SpeedOfLight * SpeedOfLight / Math.Pow(metres, 5);
        double exponent = PlanckConstant * SpeedOfLight / (metres * BoltzmannConstant * temperature);

        return front / (Math.Exp(exponent) - 1);
    }
}
