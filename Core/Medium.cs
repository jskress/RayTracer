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
    /// This property holds a plain multiplier on both of the above, so that how much of the stuff
    /// there is may be said separately from what the stuff does.  Thinning a fog is then one number
    /// rather than several, and it is the number that will be allowed to vary from place to place
    /// when a medium may be given a shape.
    /// </summary>
    public double Density { get; set; } = 1;

    /// <summary>
    /// This property reports whether this medium does anything at all, so that a scene which names
    /// one but leaves it empty pays nothing for it.
    /// </summary>
    public bool Affects => Density > 0 && (Absorbs || Emits);

    /// <summary>
    /// This property reports whether the medium's own light would pile up without limit if the
    /// space it filled had no end.  It emits somewhere that it does not absorb, so there is nothing
    /// to settle it at any value: over an endless span, such a medium is infinitely bright.  It is
    /// a perfectly good description of a bounded thing and no description at all of the sky.
    /// </summary>
    public bool MustBeBounded =>
        Emission.Red > 0 && Absorption.Red <= 0 ||
        Emission.Green > 0 && Absorption.Green <= 0 ||
        Emission.Blue > 0 && Absorption.Blue <= 0;

    private bool Absorbs => Absorption.Red > 0 || Absorption.Green > 0 || Absorption.Blue > 0;
    private bool Emits => Emission.Red > 0 || Emission.Green > 0 || Emission.Blue > 0;

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
            SurvivingFraction(Absorption.Red, distance),
            SurvivingFraction(Absorption.Green, distance),
            SurvivingFraction(Absorption.Blue, distance));
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

        double red = SurvivingFraction(Absorption.Red, distance);
        double green = SurvivingFraction(Absorption.Green, distance);
        double blue = SurvivingFraction(Absorption.Blue, distance);

        return new Color(
            behind.Red * red + AddedAlong(Absorption.Red, Emission.Red, distance),
            behind.Green * green + AddedAlong(Absorption.Green, Emission.Green, distance),
            behind.Blue * blue + AddedAlong(Absorption.Blue, Emission.Blue, distance),
            Covering(behind.Alpha, (red + green + blue) / 3));
    }

    /// <summary>
    /// This method returns how much of one color survives a crossing of the given length.
    /// </summary>
    /// <param name="absorption">How much of this color the medium absorbs per unit of distance.</param>
    /// <param name="distance">How far the light travels through the medium.</param>
    /// <returns>The fraction of this color that gets through.</returns>
    private double SurvivingFraction(double absorption, double distance)
    {
        double sigma = absorption * Density;

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
    /// <param name="emission">How much of this color the medium emits per unit of distance.</param>
    /// <param name="distance">How far the ray travels through the medium.</param>
    /// <returns>The light of this color the medium adds.</returns>
    private double AddedAlong(double absorption, double emission, double distance)
    {
        double sigma = absorption * Density;
        double epsilon = emission * Density;

        if (epsilon <= 0)
            return 0;

        // With nothing absorbing this color, the form below is a nothing over a nothing.  Its limit
        // there is what it plainly ought to be: light put in at a steady rate, with nothing taking
        // any of it out again, simply piles up with the distance.
        if (sigma <= 0)
            return epsilon * distance;

        double crossings = sigma * distance;

        // What is wanted is (ε/σ)(1 - exp(-σd)), which serves once the exponent is of any size but
        // falls apart when it is small: the exponential is then a whisker under one, subtracting it
        // from one throws away the very digits that carried the answer, and multiplying what little
        // is left by the large ε/σ makes noise of it.  (.NET's ExpM1 is no help here; it hands back
        // the same value the subtraction does.)  Below the crossover the series is used instead,
        // which is the same quantity written so that nothing has to cancel.  The two agree to a dozen
        // digits where they meet, and between them a scene may tune its absorption down toward
        // nothing and watch the picture change smoothly rather than break up.
        return crossings < 1e-4
            ? epsilon * distance * (1 - crossings / 2 + crossings * crossings / 6)
            : epsilon / sigma * (1 - Math.Exp(-crossings));
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
