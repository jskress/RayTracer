using System.Numerics;
using RayTracer.Basics;
using RayTracer.Geometry;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a film on a surface, thinner than light's own wavelength -- the skin of a
/// soap bubble, oil on a puddle, the coating on a lens -- which colors what the surface mirrors by
/// interference.
/// <para>
/// **Light meeting such a film is mirrored twice**, once off its outer face and once off its inner one,
/// and the two reflections leave together.  Where one has fallen behind the other by a whole number of
/// waves they add, and where by half a wave they cancel.  How far behind it falls depends on how thick
/// the film is and how slantwise the light crosses it, measured against the light's own wavelength, so
/// a film a few hundred nanometers thick mirrors some colors and turns others away: a bubble's swirls
/// are the film draining and thinning, and the colors run round as the angle changes.
/// </para>
/// <para>
/// **What it mirrors takes the place of Fresnel's share, color by color**, and what it does not mirror
/// goes on -- into an opaque surface's own color, or on through a clear one.  So the light let through
/// is the complement of the light mirrored, which is why a bubble seen against something bright shows
/// faint colors of the opposite kind.  Nothing here absorbs; the film only shares light out.
/// </para>
/// <para>
/// It is worked out exactly, by Airy's sum of every reflection back and forth across the film, for
/// both polarizations of light, averaged; a film thinner than its index allows to be entered at a
/// slant (total internal reflection) is handled by the same arithmetic in complex numbers.  It is
/// worked out over the spectrum's bands, and converted back to a color for a render carrying only
/// three.  The surface beneath the film is whatever the material is made of, by its index of
/// refraction, and the film's own index is its <see cref="IndexOfRefraction"/>.
/// </para>
/// </summary>
public class ThinFilm
{
    /// <summary>
    /// This is the index of refraction a film is given when it names none: soapy water's.
    /// </summary>
    public const double DefaultIndexOfRefraction = 1.33;

    /// <summary>
    /// This is the most samples taken across one band.  Past this a film is so thick that its colors
    /// have run together into white, as a real one's do -- the colors of a thick film are too finely
    /// spaced along the spectrum for the eye to tell apart.
    /// </summary>
    private const int MostSamples = 32;

    /// <summary>
    /// This property holds how thick the film is, in nanometers; or, where a pattern sets it, how thin
    /// it is at its thinnest.
    /// </summary>
    public double Thickness { get; set; }

    /// <summary>
    /// This property holds how thick the film is at its thickest, in nanometers, where a pattern sets
    /// its thickness.  Unused otherwise.
    /// </summary>
    public double Thickest { get; set; }

    /// <summary>
    /// This property holds the pattern that sets the film's thickness from place to place, from
    /// <see cref="Thickness"/> where its value is nought to <see cref="Thickest"/> where it is one;
    /// or <c>null</c>, for a film the same thickness everywhere.  It is read in the space of the
    /// surface, as a pigment is.
    /// </summary>
    public DensityShape Pattern { get; set; }

    /// <summary>
    /// This property holds the film's own index of refraction.
    /// </summary>
    public double IndexOfRefraction { get; set; } = DefaultIndexOfRefraction;

    /// <summary>
    /// This method returns how thick the film is at a point.
    /// </summary>
    /// <param name="point">The point, in the space of the surface; it may be <c>null</c> for a film
    /// with no pattern.</param>
    /// <returns>The thickness there, in nanometers.</returns>
    public double ThicknessAt(Point point)
    {
        return Pattern is null
            ? Thickness
            : Thickness + (Thickest - Thickness) * Pattern.ValueAt(point);
    }

    /// <summary>
    /// This method works out how the film shares out the light at a hit: what it mirrors, what it lets
    /// on, and the color its highlight takes.
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="intersection">The hit, prepared.</param>
    /// <returns>The shares.</returns>
    public FilmShares<TS> SharesAt<TS>(Intersection intersection)
        where TS : struct, ISpectrum<TS>
    {
        double thickness = ThicknessAt(Pattern is null
            ? null
            : intersection.Surface.WorldToSurface(intersection.Point, 0, intersection.Portals));

        return SharesAt<TS>(
            thickness, intersection.Eye.Dot(intersection.Normal), intersection.N1, intersection.N2);
    }

    /// <summary>
    /// This method works out how the film shares out the light meeting it at a given angle.
    /// <para>
    /// The highlight is a lamp's mirror image, and so it takes the film's color too.  How bright it is
    /// stays the material's to say, as it always has, so the color is taken against what the film
    /// mirrors on average over every thickness -- which is what a film too thick to show colors
    /// mirrors, and white.  Where the film cancels every color, as a bubble does just before it bursts,
    /// the highlight goes with them.
    /// </para>
    /// </summary>
    /// <typeparam name="TS">The kind of light being carried.</typeparam>
    /// <param name="thickness">How thick the film is there, in nanometers.</param>
    /// <param name="cosAngle">The cosine of the angle the light meets the surface at.</param>
    /// <param name="outside">The index of refraction on the side the light comes from.</param>
    /// <param name="beneath">The index of refraction of what is under the film.</param>
    /// <returns>The shares.</returns>
    public FilmShares<TS> SharesAt<TS>(double thickness, double cosAngle, double outside, double beneath)
        where TS : struct, ISpectrum<TS>
    {
        Span<double> perBand = stackalloc double[SpectralColor.Bands];
        double average = ReflectanceAt(thickness, cosAngle, outside, beneath, perBand);
        TS mirrored = TS.FromSampled(perBand);
        TS highlight = average > 1e-12 ? mirrored * (1 / average) : TS.White;

        return new FilmShares<TS>(mirrored, TS.White - mirrored, highlight);
    }

    /// <summary>
    /// This method works out how much of the light meeting the film it mirrors, band by band.
    /// </summary>
    /// <param name="thickness">How thick the film is there, in nanometers.</param>
    /// <param name="cosAngle">The cosine of the angle the light meets the surface at.</param>
    /// <param name="outside">The index of refraction on the side the light comes from.</param>
    /// <param name="beneath">The index of refraction of what is under the film.</param>
    /// <param name="perBand">Where to put the share mirrored in each band.</param>
    /// <returns>What the film mirrors on average over every thickness: the share a film too thick to
    /// show colors would mirror.</returns>
    public double ReflectanceAt(
        double thickness, double cosAngle, double outside, double beneath, Span<double> perBand)
    {
        double film = IndexOfRefraction;

        // Exactly at a graze the arithmetic for one polarization meets nought over nought, so the
        // light is kept a hair short of it; nothing could tell the difference.
        double cos0 = Math.Clamp(cosAngle, 1e-12, 1);
        double sinSquared = 1 - cos0 * cos0;

        // The cosines of the angles inside the film and beneath it, by Snell's law.  Past the angle at
        // which light can enter either, the cosine is imaginary: the light does not cross, but reaches
        // a little way in and dies away, and a film thin enough is crossed by that tail.  The root
        // taken is the one whose imaginary part is positive, which makes the tail die away.
        Complex cos1 = Complex.Sqrt(1 - outside * outside / (film * film) * sinSquared);
        Complex cos2 = Complex.Sqrt(1 - outside * outside / (beneath * beneath) * sinSquared);

        // How much each face mirrors, as waves rather than as shares of light, for light polarized
        // across the plane it travels in and along it.
        Complex s01 = Face(outside * cos0, film * cos1);
        Complex s12 = Face(film * cos1, beneath * cos2);
        Complex p01 = Face(film * cos0, outside * cos1);
        Complex p12 = Face(beneath * cos1, film * cos2);

        // How far behind the second reflection falls, as an angle, is this over the wavelength.
        Complex lag = 4 * Math.PI * film * thickness * cos1;

        // Enough samples across a band that the reflection's swing through its colors is followed:
        // at least four to each swing, where the swings are closest, at the shortest wavelengths.
        const double bandWidth = (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) /
                                 SpectralColor.Bands;
        double shortest = SpectralColor.ShortestWavelength;
        double swing = Math.Abs(lag.Real) * bandWidth / (shortest * shortest);
        int samples = Math.Clamp((int) Math.Ceiling(swing / (Math.PI / 2)), 1, MostSamples);

        for (int band = 0; band < SpectralColor.Bands; band++)
        {
            double start = SpectralColor.WavelengthOf(band) - bandWidth / 2;
            double sum = 0;

            for (int sample = 0; sample < samples; sample++)
            {
                double wavelength = start + (sample + 0.5) * bandWidth / samples;
                Complex turn = Complex.Exp(Complex.ImaginaryOne * lag / wavelength);

                sum += (Airy(s01, s12, turn) + Airy(p01, p12, turn)) / 2;
            }

            perBand[band] = sum / samples;
        }

        return (Incoherent(s01, s12) + Incoherent(p01, p12)) / 2;
    }

    /// <summary>
    /// This method returns how much of the light meeting one face is mirrored, as a wave: Fresnel's
    /// coefficient, from what each side of the face offers the light.
    /// </summary>
    /// <param name="near">What the near side offers.</param>
    /// <param name="far">What the far side offers.</param>
    /// <returns>The coefficient.</returns>
    private static Complex Face(Complex near, Complex far)
    {
        Complex sum = near + far;

        return sum == Complex.Zero ? Complex.Zero : (near - far) / sum;
    }

    /// <summary>
    /// This method sums every reflection back and forth across the film, as waves, and returns the
    /// share of light that leaves -- Airy's formula.
    /// </summary>
    /// <param name="outer">The outer face's coefficient.</param>
    /// <param name="inner">The inner face's coefficient.</param>
    /// <param name="turn">How the wave is turned by crossing the film and back.</param>
    /// <returns>The share of the light mirrored.</returns>
    private static double Airy(Complex outer, Complex inner, Complex turn)
    {
        Complex delayed = inner * turn;
        Complex denominator = 1 + outer * delayed;
        double share = denominator == Complex.Zero
            ? 1
            : ((outer + delayed) / denominator).Magnitude;

        return Math.Min(share * share, 1);
    }

    /// <summary>
    /// This method returns the share of light the film mirrors averaged over every thickness: the
    /// two faces' shares added as light rather than as waves, with every reflection between them.
    /// </summary>
    /// <param name="outer">The outer face's coefficient.</param>
    /// <param name="inner">The inner face's coefficient.</param>
    /// <returns>The share of the light mirrored.</returns>
    private static double Incoherent(Complex outer, Complex inner)
    {
        double first = Math.Min(outer.Magnitude * outer.Magnitude, 1);
        double second = Math.Min(inner.Magnitude * inner.Magnitude, 1);
        double between = 1 - first * second;

        return between <= 0 ? 1 : (first + second - 2 * first * second) / between;
    }
}

/// <summary>
/// This struct holds how a film shares out the light meeting it: what it mirrors, what it lets on --
/// all the rest -- and the color it lends a highlight.
/// </summary>
/// <param name="Mirrored">The share mirrored, band by band.</param>
/// <param name="Passed">The share let on, which is the rest.</param>
/// <param name="Highlight">The color a highlight takes: the share mirrored, against what the film
/// mirrors on average.</param>
/// <typeparam name="TS">The kind of light being carried.</typeparam>
public readonly record struct FilmShares<TS>(TS Mirrored, TS Passed, TS Highlight)
    where TS : struct, ISpectrum<TS>;
