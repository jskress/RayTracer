using RayTracer.Basics;
using RayTracer.Pigments;
using RayTracer.Fields;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents what fills a piece of space, rather than what bounds it: something a ray
/// passes <i>through</i>, which may take light out of it and may put light of its own in.
/// <para>
/// Two things happen to a ray crossing a span of a medium.  Light is absorbed, so what lies beyond
/// arrives dimmer -- and dimmer color by color, which is why a haze can be blue rather than merely
/// gray.  And the medium's own light is added all along the way, each bit of it dimmed in turn by
/// however much medium still lies between it and the eye.  With the density the same throughout,
/// both have an answer that can simply be written down, so nothing here is sampled or stepped
/// along; a span costs one exponential per color.
/// </para>
/// <para>
/// Written out, for a span of length <c>d</c>, where <c>σ</c> is what the medium absorbs per unit of
/// distance and <c>ε</c> what it emits:
/// </para>
/// <code>
///     T = exp(-σd)                      how much of what is behind gets through
///     C = C(behind)·T + (ε/σ)·(1 - T)    what the eye is handed
/// </code>
/// <para>
/// That second line is worth reading twice, because it says the answer is a plain mix between what
/// lies beyond and the fixed color <c>ε/σ</c>, with the transmittance as the mixing fraction.  The
/// distance fog of every real-time renderer -- blend toward a fog color by how far you looked -- is
/// not an approximation of this; for a medium of even density it <i>is</i> this, with the fog color
/// being a thing the medium's own numbers imply rather than a knob set by hand.
/// </para>
/// <para>
/// It also settles what a ray that never hits anything should come back with.  Let the span run to
/// infinity and the mix completes: the transmittance falls to nothing and the eye is handed exactly
/// <c>ε/σ</c>.  So an endless haze both swallows the sky and becomes it, with nothing needing to be
/// clamped or special-cased.  A medium that absorbs without emitting turns that sky black, which is
/// the honest answer for a fog that has no light of its own.
/// </para>
/// <para>
/// This medium neither scatters nor varies from place to place.  Scattering -- light arriving from a
/// lamp off to one side and being turned toward the eye, which is what a shaft of light through a
/// window is -- is what comes next, and needs the medium to be sampled along the ray rather than
/// answered in closed form.
/// </para>
/// </summary>
public class Medium
{
    /// <summary>
    /// This property holds how much light the medium takes out of a ray for each unit of distance
    /// it travels, color by color.  A larger number in one color absorbs that color more strongly,
    /// so what comes through is tinted toward the colors left behind: a medium absorbing red most
    /// heavily looks blue-green.  The alpha channel takes no part.
    /// </summary>
    public Color Absorption { get; set; } = Colors.Black;

    /// <summary>
    /// This property holds how much light the medium adds for each unit of distance, color by
    /// color.  It is the medium shining of itself -- a flame, a glowing gas, or the daylight a haze
    /// carries -- and not light borrowed from any lamp.
    /// </summary>
    public Color Emission { get; set; } = Colors.Black;

    /// <summary>
    /// This property holds what the medium gives off from place to place, when what it gives off
    /// is not the same everywhere.
    /// <para>
    /// It is a pigment rather than a field because what varies is a <i>color</i>, and a pigment is
    /// already the thing that answers what color a place is.  A flame is the case it was added for:
    /// white at the heart, yellow above that and red at the tip, which is most of what makes fire
    /// read as fire and which one flat color cannot say at all.
    /// </para>
    /// </summary>
    public Pigment EmissionPigment { get; set; }

    /// <summary>
    /// This property holds how much light the medium turns aside for each unit of distance, color by
    /// color -- light that came from somewhere else and leaves in a new direction.
    /// <para>
    /// It costs a ray twice over, and the two are worth keeping apart.  Light turned aside is light
    /// that no longer continues along the ray, so scattering dims what lies beyond exactly as
    /// absorption does; that is why <see cref="ExtinctionOf"/> counts both.  But unlike absorbed
    /// light, it has not gone anywhere -- it went somewhere else, and where some of it goes is toward
    /// the eye, gathered from every lamp along the way.  That second half is what a shaft of light
    /// through a window is, and it is the one term here with no exact answer to be written down.
    /// </para>
    /// </summary>
    public Color Scattering { get; set; } = Colors.Black;

    /// <summary>
    /// This property holds which way the medium prefers to turn light, from minus one to one.  At
    /// nothing it has no preference and spreads light evenly.  Above nothing it favors carrying light
    /// on the way it was already going, which is what nearly everything real does -- it is why fog
    /// glows brightest around a lamp you are looking toward, and why cloud edges light up against the
    /// sun.  Below nothing it favors sending light back the way it came.
    /// </summary>
    public double Anisotropy { get; set; }

    /// <summary>
    /// This property holds which shape of scattering the medium follows.  The default covers both an
    /// even spread and the whole forward-and-back family, by way of <see cref="Anisotropy"/>; the
    /// other is for the particular case of particles far smaller than the wavelength of the light,
    /// which is what makes the sky blue.
    /// </summary>
    public PhaseFunction PhaseFunction { get; set; } = PhaseFunction.HenyeyGreenstein;

    /// <summary>
    /// This property holds how many further turns of a light's path through the medium are followed
    /// past the first.  It is nothing by default, which is to say only light that reached a place
    /// straight from a lamp is counted.
    /// <para>
    /// That is the whole of why a thick medium comes out too dark and too gray.  Nearly all the light
    /// leaving a cloud has been turned a dozen times or more on its way out, and none of that is here
    /// until this is raised: what a cloud does to light is mostly what it does to light it has already
    /// turned once.
    /// </para>
    /// </summary>
    public int Bounces { get; set; }

    /// <summary>
    /// This property holds the share of stopped light that carried on rather than being swallowed,
    /// color by color.  It is what a turn of the path is worth: a medium that absorbs nothing passes
    /// all of it on and can be turned any number of times without loss, while one that absorbs half of
    /// what it stops is down to a sixteenth after four turns.
    /// <para>
    /// Note that the density falls out of it entirely, both parts of the fraction being in proportion
    /// to how much stuff is there, so this is a property of what the stuff <i>is</i> and not of how
    /// much of it there happens to be at one place or another.
    /// </para>
    /// </summary>
    public Color Albedo => new (
        ShareCarriedOn(Absorption.Red, Scattering.Red),
        ShareCarriedOn(Absorption.Green, Scattering.Green),
        ShareCarriedOn(Absorption.Blue, Scattering.Blue));

    /// <summary>
    /// This property holds how many places along a ray's crossing the medium is asked what light
    /// reaches it.  Only scattering needs this; what a medium absorbs and gives off is answered
    /// exactly, at no cost per sample at all.
    /// </summary>
    public int Samples { get; set; } = 16;

    /// <summary>
    /// This property holds a plain multiplier on all three coefficients, so that how much of the
    /// stuff there is may be said separately from what the stuff does.  Thinning a fog is then one
    /// number rather than several.  Where the medium has a shape, this scales the whole of it.
    /// </summary>
    public double Density { get; set; } = 1;

    /// <summary>
    /// This property holds how much of the stuff there is from place to place, if it is not the same
    /// throughout: a compiled function of where you are, in the space of whatever surface the medium
    /// fills.  It is nothing by default, which is an even density everywhere.
    /// <para>
    /// This is the one property that changes how the rest are worked out.  With the density even, what
    /// survives a crossing and what the medium gives off along it both have exact answers that can be
    /// written down.  Let it vary and neither does: what survives becomes the exponential of an
    /// integral along the ray, and the light given off is weighed by that at every point.  So a medium
    /// with a shape is marched -- walked along in steps -- and one without is not, which is both
    /// quicker and what keeps every scene written before shapes existed rendering exactly as it did.
    /// </para>
    /// </summary>
    public FieldFunction DensityField { get; set; }

    /// <summary>
    /// This property holds how much of the stuff there is from place to place when the shape is written
    /// as one of the pattern library's patterns rather than as a function.
    /// <para>
    /// It is the same job as <see cref="DensityField"/> and the two are alternatives, never both: a
    /// function is the way to say a shape exactly, and a pattern is the way to say one that would be
    /// tedious to write down but that the library already knows -- a granite's grain, a marble's veins,
    /// the cells of a crackle.  Which one a scene reaches for is a matter of what it is trying to say.
    /// </para>
    /// </summary>
    public DensityShape DensityPattern { get; set; }

    /// <summary>
    /// This property reports whether the medium's density varies from place to place, and so whether a
    /// crossing of it has to be marched rather than answered.
    /// </summary>
    public bool HasShape => DensityVaries || EmissionPigment is not null;

    /// <summary>
    /// This property reports whether it is the <i>amount</i> of the stuff that differs from place
    /// to place, as against the light it gives off.  Both have to be walked along rather than
    /// written down, so <see cref="HasShape"/> covers the two together -- but only this one takes
    /// away the floor under how much stuff a crossing must pass through, and that is what an
    /// endless crossing rests on.  The two are asked apart so that whoever is turned away is told
    /// which of them they wrote.
    /// </summary>
    public bool DensityVaries => DensityField is not null || DensityPattern is not null;

    /// <summary>
    /// This method returns how much of the stuff there is at the given place.  A shape that would go
    /// negative is taken as empty: a density below nothing has no meaning, and left alone it would
    /// have a ray gaining light for crossing the stuff rather than losing it.
    /// </summary>
    /// <param name="point">Where to ask, in the space of the surface the medium fills.</param>
    /// <returns>The density there.</returns>
    /// <summary>
    /// This method returns what the medium gives off at the given place.
    /// </summary>
    /// <param name="point">Where to ask, in the space of the surface the medium fills.</param>
    /// <returns>What it gives off there.</returns>
    public Color EmissionAt(Point point)
    {
        return EmissionPigment is null ? Emission : EmissionPigment.GetTransformedColorFor(point);
    }

    public double DensityAt(Point point)
    {
        if (DensityField is not null)
            return Density * Math.Max(0, DensityField.Evaluate(point.X, point.Y, point.Z));

        if (DensityPattern is not null)
            return Density * Math.Max(0, DensityPattern.ValueAt(point));

        return Density;
    }

    /// <summary>
    /// This method returns how much light stops coming this way per unit of distance at a place of the
    /// given density, color by color.
    /// </summary>
    /// <param name="density">How much of the stuff there is at the place in question.</param>
    /// <returns>The rate at which each color leaves the ray.</returns>
    public Color ExtinctionAt(double density)
    {
        return new Color(
            (Absorption.Red + Scattering.Red) * density,
            (Absorption.Green + Scattering.Green) * density,
            (Absorption.Blue + Scattering.Blue) * density);
    }

    /// <summary>
    /// This property reports whether this medium does anything at all, so that a scene which names
    /// one but leaves it empty pays nothing for it.
    /// </summary>
    public bool Affects => Density > 0 && (Absorbs || Emits || Scatters);

    /// <summary>
    /// This property reports whether the medium turns light aside at all, and so whether the one
    /// term that has to be sampled needs to be worked out.  A medium that does not is exactly as
    /// cheap as it was before scattering existed.
    /// </summary>
    public bool Scatters =>
        Density > 0 && (Scattering.Red > 0 || Scattering.Green > 0 || Scattering.Blue > 0);

    /// <summary>
    /// This property reports whether the medium's own light would pile up without limit if the
    /// space it filled had no end.  It emits somewhere that nothing takes light back out, so there is
    /// nothing to settle it at any value: over an endless span, such a medium is infinitely bright.
    /// It is a perfectly good description of a bounded thing and no description at all of the sky.
    /// Note that turning light aside settles it just as absorbing it does, both being ways for light
    /// to stop coming this way.
    /// </summary>
    public bool MustBeBounded =>
        // A pigment is asked about a point and cannot be asked whether it is ever anything but
        // black, so one is taken as emitting in every color.  Erring this way turns away a medium
        // that might have been harmless; erring the other way lets an endless one through, and
        // that is a picture of infinity rather than a picture of anything.
        (EmissionPigment is not null || Emission.Red > 0) &&
            ExtinctionOf(Absorption.Red, Scattering.Red) <= 0 ||
        (EmissionPigment is not null || Emission.Green > 0) &&
            ExtinctionOf(Absorption.Green, Scattering.Green) <= 0 ||
        (EmissionPigment is not null || Emission.Blue > 0) &&
            ExtinctionOf(Absorption.Blue, Scattering.Blue) <= 0;

    /// <summary>
    /// This method returns what share of the light stopped in one color carried on rather than being
    /// swallowed.
    /// </summary>
    /// <param name="absorption">How much of this color the medium absorbs per unit of distance.</param>
    /// <param name="scattering">How much of this color it turns aside per unit of distance.</param>
    /// <returns>The share that carried on, from nothing up to one.</returns>
    private static double ShareCarriedOn(double absorption, double scattering)
    {
        double stopped = absorption + scattering;

        return stopped > 0 ? scattering / stopped : 0;
    }

    /// <summary>
    /// This method picks a direction for light to have arrived from, in proportion to how much of it
    /// the medium would turn from there toward the way the ray is going.  Drawing from the shape
    /// itself is what makes a single direction stand for all of them: the directions that matter most
    /// are picked most often, and each one picked may then be counted at its face value.
    /// </summary>
    /// <param name="heading">The way the light being followed is travelling.</param>
    /// <param name="alongTheCone">A number from nothing up to one, which picks the angle.</param>
    /// <param name="aroundIt">A number from nothing up to one, which picks the way round.</param>
    /// <returns>The direction the light came from.</returns>
    public Vector SampleDirectionAround(Vector heading, double alongTheCone, double aroundIt)
    {
        double cosine = PhaseFunction == Core.PhaseFunction.Rayleigh
            ? RayleighCosine(alongTheCone)
            : HenyeyGreensteinCosine(alongTheCone);
        double sine = Math.Sqrt(Math.Max(0, 1 - cosine * cosine));
        double around = 2 * Math.PI * aroundIt;

        // A pair of directions square to the heading and to each other, to swing the picked angle
        // about.  Which pair hardly matters; what matters is that they are square, and that the one
        // chosen to start from is not itself nearly the heading, or squaring it against it would be
        // all rounding error.
        Vector aside = Math.Abs(heading.X) < 0.9
            ? new Vector(1, 0, 0)
            : new Vector(0, 1, 0);
        Vector first = heading.Cross(aside).Unit;
        Vector second = heading.Cross(first);

        return (first * (sine * Math.Cos(around)) +
                second * (sine * Math.Sin(around)) +
                heading * cosine).Unit;
    }

    /// <summary>
    /// This method returns the cosine of a Henyey-Greenstein turn, picked in proportion to the shape.
    /// It is the shape's own sum, read backward: the standard inversion of it.
    /// </summary>
    /// <param name="fraction">A number from nothing up to one.</param>
    /// <returns>The cosine of the angle turned through.</returns>
    private double HenyeyGreensteinCosine(double fraction)
    {
        if (Anisotropy == 0)
            return 1 - 2 * fraction;

        double squared = Anisotropy * Anisotropy;
        double inner = (1 - squared) / (1 - Anisotropy + 2 * Anisotropy * fraction);

        return (1 + squared - inner * inner) / (2 * Anisotropy);
    }

    /// <summary>
    /// This method returns the cosine of a Rayleigh turn, picked in proportion to the shape.  Its sum
    /// read backward is a cubic, and this is Cardano's answer to it, tidied by the fact that the two
    /// cube roots multiply to minus one.
    /// </summary>
    /// <param name="fraction">A number from nothing up to one.</param>
    /// <returns>The cosine of the angle turned through.</returns>
    private static double RayleighCosine(double fraction)
    {
        double shifted = 4 * fraction - 2;
        double root = Math.Cbrt(shifted + Math.Sqrt(shifted * shifted + 1));

        return root - 1 / root;
    }

    private bool Absorbs => Absorption.Red > 0 || Absorption.Green > 0 || Absorption.Blue > 0;
    private bool Emits =>
        EmissionPigment is not null ||
        Emission.Red > 0 || Emission.Green > 0 || Emission.Blue > 0;

    /// <summary>
    /// This method returns how much light of one color stops coming this way per unit of distance,
    /// which is everything absorbed plus everything turned aside.
    /// </summary>
    /// <param name="absorption">How much of this color the medium absorbs per unit of distance.</param>
    /// <param name="scattering">How much of this color it turns aside per unit of distance.</param>
    /// <returns>The rate at which this color leaves the ray.</returns>
    private double ExtinctionOf(double absorption, double scattering)
    {
        return (absorption + scattering) * Density;
    }

    /// <summary>
    /// This method returns how much of the light arriving at a point from one direction leaves it in
    /// another, for the angle between the two.  It is measured against an even spread, which is to say
    /// an even spread hands back one in every direction and anything else hands back more one way and
    /// less another, averaging to one over the whole sphere.
    /// <para>
    /// A density over directions would hand back a four pi'th of that -- and it is worth saying why it
    /// does not.  Nothing else about the way this renderer lights a scene is normalized that way: a
    /// lamp's brightness does not fall off with distance, and a matte surface facing a lamp returns
    /// the lamp's color rather than a pi'th of it.  Dropping a properly normalized phase function into
    /// the middle of that buys no physical truth; it only makes a scene write a scattering of three
    /// where it means a third, and a lamp of three where it means one.  What carries the physics here
    /// is the <i>shape</i> -- which way light prefers to go, and by how much -- and that is the same
    /// either way.  An integrator that gathers light over the whole sphere rather than from a handful
    /// of lamps, as multiple scattering will, must divide by four pi to use these.
    /// </para>
    /// </summary>
    /// <param name="cosine">The cosine of the angle between the way the light was going and the way
    /// the scattered light goes.  One is straight on, minus one is straight back.</param>
    /// <returns>How much of the light goes that way.</returns>
    public double PhaseFor(double cosine)
    {
        return PhaseFunction switch
        {
            // Rayleigh's, for particles far smaller than the light's own wavelength.  It is gently
            // two-lobed -- as much goes back as goes on, and least goes out to the sides.
            PhaseFunction.Rayleigh => 3 * (1 + cosine * cosine) / 4,
            _ => HenyeyGreensteinFor(cosine)
        };
    }

    /// <summary>
    /// This method returns the Henyey-Greenstein value for the given angle.  It is the standard
    /// one-parameter family, and the reason a single number covers both an even spread and every
    /// degree of forward or backward preference: at an anisotropy of nothing it reduces exactly to
    /// the even spread.
    /// </summary>
    /// <param name="cosine">The cosine of the scattering angle.</param>
    /// <returns>How much of the light goes that way.</returns>
    private double HenyeyGreensteinFor(double cosine)
    {
        if (Anisotropy == 0)
            return 1;

        double squared = Anisotropy * Anisotropy;
        double denominator = 1 + squared - 2 * Anisotropy * cosine;

        return (1 - squared) / (denominator * Math.Sqrt(denominator));
    }

    /// <summary>
    /// This property holds one rate standing for all three colors, which is what the places to ask
    /// about scattering are chosen by.  Choosing them once rather than three times over is what keeps
    /// a crossing to one set of shadow rays; each color is then weighed for its own extinction, so
    /// nothing is lost by having chosen with the average.
    /// </summary>
    public double MeanExtinction =>
        (ExtinctionOf(Absorption.Red, Scattering.Red) +
         ExtinctionOf(Absorption.Green, Scattering.Green) +
         ExtinctionOf(Absorption.Blue, Scattering.Blue)) / 3;

    /// <summary>
    /// This method returns what fraction of light fails to survive the given number of crossings'
    /// worth of medium -- one less the exponential of minus it.
    /// <para>
    /// Written plainly that loses nearly all its precision when the exponent is small: the exponential
    /// is a whisker under one, and subtracting it from one throws away the digits that carried the
    /// answer.  Whatever it is then divided by makes noise of what is left.  (.NET's ExpM1 is no help;
    /// measured, it hands back the very same value the subtraction does.)  Below the crossover the
    /// series is used instead, which is the same quantity written so that nothing has to cancel.
    /// </para>
    /// </summary>
    /// <param name="crossings">How many times over the light might have been stopped.</param>
    /// <returns>The fraction stopped, from nothing up to one.</returns>
    public static double FractionStopped(double crossings)
    {
        if (double.IsPositiveInfinity(crossings))
            return 1;

        return crossings < 1e-4
            ? crossings * (1 - crossings / 2 + crossings * crossings / 6)
            : 1 - Math.Exp(-crossings);
    }

    /// <summary>
    /// This method returns the fraction of light that survives a crossing of the given length, color
    /// by color.  It is what a shadow ray needs: how much of a lamp arrives through the stuff in the
    /// way.  What the medium itself gives off has no part in that, a lamp's light being the only
    /// thing asked after.
    /// </summary>
    /// <param name="distance">How far the light travels through the medium.</param>
    /// <returns>The fraction of each color that gets through.</returns>
    public Color GetTransmittanceOver(double distance)
    {
        if (!Affects || distance <= 0)
            return Colors.White;

        return new Color(
            SurvivingFraction(Absorption.Red, Scattering.Red, distance),
            SurvivingFraction(Absorption.Green, Scattering.Green, distance),
            SurvivingFraction(Absorption.Blue, Scattering.Blue, distance));
    }

    /// <summary>
    /// This method returns what the eye is handed after a crossing of the given length: the color
    /// behind the medium, dimmed by what the crossing cost it, with what the medium gave off along
    /// the way added on.
    /// </summary>
    /// <param name="behind">The color arriving from beyond the medium.</param>
    /// <param name="distance">How far the ray travels through the medium; may be infinite, for a
    /// ray that crosses the medium and strikes nothing at all.</param>
    /// <returns>The color to hand back in its place.</returns>
    public Color ApplyOver(Color behind, double distance)
    {
        if (!Affects || distance <= 0)
            return behind;

        double red = SurvivingFraction(Absorption.Red, Scattering.Red, distance);
        double green = SurvivingFraction(Absorption.Green, Scattering.Green, distance);
        double blue = SurvivingFraction(Absorption.Blue, Scattering.Blue, distance);

        return new Color(
            behind.Red * red + AddedAlong(Absorption.Red, Scattering.Red, Emission.Red, distance),
            behind.Green * green + AddedAlong(Absorption.Green, Scattering.Green, Emission.Green, distance),
            behind.Blue * blue + AddedAlong(Absorption.Blue, Scattering.Blue, Emission.Blue, distance),
            Covering(behind.Alpha, (red + green + blue) / 3));
    }

    /// <summary>
    /// This method returns how much of one color survives a crossing of the given length.
    /// </summary>
    /// <param name="absorption">How much of this color the medium absorbs per unit of distance.</param>
    /// <param name="scattering">How much of this color it turns aside per unit of distance.</param>
    /// <param name="distance">How far the light travels through the medium.</param>
    /// <returns>The fraction of this color that gets through.</returns>
    private double SurvivingFraction(double absorption, double scattering, double distance)
    {
        double sigma = ExtinctionOf(absorption, scattering);

        // An endless span needs no case of its own here: the exponential of minus infinity is
        // nothing, which is exactly what gets through an endless absorbing fog.
        return sigma <= 0 ? 1 : Math.Exp(-sigma * distance);
    }

    /// <summary>
    /// This method returns how much light of one color the medium gave off along a crossing of the
    /// given length, each bit of it already dimmed by however much medium lay between it and this
    /// end of the span.
    /// </summary>
    /// <param name="absorption">How much of this color the medium absorbs per unit of distance.</param>
    /// <param name="scattering">How much of this color it turns aside per unit of distance.</param>
    /// <param name="emission">How much of this color the medium emits per unit of distance.</param>
    /// <param name="distance">How far the ray travels through the medium.</param>
    /// <returns>The light of this color the medium adds.</returns>
    private double AddedAlong(
        double absorption, double scattering, double emission, double distance)
    {
        // What settles the medium's own light is everything that stops light coming this way, not
        // absorption alone: a medium that turns its own glow aside is as dimmed by that as by
        // swallowing it.
        double sigma = ExtinctionOf(absorption, scattering);
        double epsilon = emission * Density;

        if (epsilon <= 0)
            return 0;

        // With nothing absorbing this color, the form below is a nothing over a nothing.  Its limit
        // there is what it plainly ought to be: light put in at a steady rate, with nothing taking
        // any of it out again, simply piles up with the distance.
        if (sigma <= 0)
            return epsilon * distance;

        // (ε/σ)(1 - exp(-σd)), with the awkward part of it kept in one place.
        return epsilon / sigma * FractionStopped(sigma * distance);
    }

    /// <summary>
    /// This method returns how much of the pixel is covered once the medium has had its say.  A
    /// medium stands in front of whatever lies beyond it, so it covers what it hides -- an image
    /// rendered over nothing at all comes out opaque wherever the fog is thick, which is right,
    /// since the fog is a thing that is there.
    /// </summary>
    /// <param name="behind">How much of the pixel whatever lies beyond the medium covered.</param>
    /// <param name="surviving">The fraction of light, on average, that the span lets through.</param>
    /// <returns>How much of the pixel is covered now.</returns>
    private static double Covering(double behind, double surviving)
    {
        return behind + (1 - behind) * (1 - surviving);
    }
}
