namespace RayTracer.Graphics;

/// <summary>
/// This class turns light described by wavelength into the three numbers the rest of the renderer
/// works in.
/// <para>
/// It exists because some things cannot be worked out in red, green and blue at all.  The sky is the
/// first of them: air scatters light in proportion to the inverse fourth power of its wavelength, so
/// what the atmosphere does to a beam depends on the whole spectrum rather than on three samples of
/// it.  At noon that hardly matters, the air being thin enough that the answer is nearly linear.  At
/// sunset light crosses some thirty-eight times as much air, and what survives is
/// <c>exp(-τ)</c> with <c>τ</c> itself running as the inverse fourth power -- a reshaping of the
/// spectrum so severe that three samples of it give a red which is plausible and wrong.
/// </para>
/// <para>
/// Working in bands and converting at the end costs nothing where it is used, because a sky is worked
/// out once for each direction and kept, rather than once for every ray.  And when this renderer one
/// day carries spectra throughout, whatever is computed here need only stop converting.
/// </para>
/// </summary>
public static class SpectralColor
{
    /// <summary>
    /// This property holds the shortest wavelength, in nanometers, that the eye is taken to answer to.
    /// </summary>
    public const double ShortestWavelength = 380;

    /// <summary>
    /// This property holds the longest wavelength, in nanometers, that the eye is taken to answer to.
    /// </summary>
    public const double LongestWavelength = 780;

    /// <summary>
    /// This property holds how many bands the visible range is cut into.  Thirty-two is well past the
    /// point where adding more changes a sky's color by anything the eye or the file format could
    /// hold, and the whole of this work happens once per render rather than once per ray.
    /// </summary>
    public const int Bands = 32;

    private const double BandWidth = (LongestWavelength - ShortestWavelength) / Bands;

    /// <summary>
    /// This method returns the wavelength, in nanometers, at the middle of the given band.
    /// </summary>
    /// <param name="band">Which band is wanted, from nothing up to one less than the count.</param>
    /// <returns>The wavelength at the middle of that band.</returns>
    public static double WavelengthOf(int band)
    {
        return ShortestWavelength + (band + 0.5) * BandWidth;
    }

    /// <summary>
    /// This property reports what each of the three matching functions comes to when summed across
    /// the bands.
    /// <para>
    /// The three should come out equal, and that they do is the sharpest check there is that the
    /// matching functions are right.  The CIE scaled them so deliberately: it is what makes light of
    /// the same strength at every wavelength come out at dead center on the chromaticity chart, and
    /// an error in any one of the fitted curves shows up here as a leaning that no amount of later
    /// scaling could hide.
    /// </para>
    /// </summary>
    public static (double Red, double Green, double Blue) MatchingTotals { get; } = WorkOutTotals();

    /// <summary>
    /// This method converts light given band by band into a color.
    /// <para>
    /// The spectrum is weighed against how strongly the eye's three kinds of cone answer to each
    /// wavelength -- the color matching functions, which are measurements of people rather than
    /// anything derivable -- and the three sums that fall out are then turned into the primaries a
    /// screen actually has.
    /// </para>
    /// <para>
    /// It is then balanced so that light of the same strength at every wavelength comes out white.
    /// That is a choice rather than a consequence, and worth knowing about: light like that is not
    /// what a screen means by white, a screen being built around a white that stands for daylight and
    /// is bluer, so left alone an even spectrum would come out at about
    /// <c>(1.20, 0.95, 0.91)</c> -- noticeably warm.  Balancing it away is what lets a spectrum and
    /// an ordinary color in a scene file agree about what white is, so that a light of one is a light
    /// of one however it was arrived at.  What it costs is that these colors are what a photographer
    /// would get having balanced for an even spectrum, rather than raw tristimulus values.
    /// </para>
    /// </summary>
    /// <param name="perBand">How much light there is in each band.</param>
    /// <returns>The color that light appears as.</returns>
    public static Color ToColor(ReadOnlySpan<double> perBand)
    {
        if (perBand.Length != Bands)
            throw new ArgumentException($"Expected {Bands} bands, and was given {perBand.Length}.");

        Color raw = ToUnbalancedColor(perBand);

        return IntoGamut(new Color(
            raw.Red * WhiteBalance.Red,
            raw.Green * WhiteBalance.Green,
            raw.Blue * WhiteBalance.Blue));
    }

    /// <summary>
    /// This method brings a color back inside what a screen can actually show.
    /// <para>
    /// Some real colors are not among them.  A screen's three primaries between them reach only part
    /// of what the eye can see, and a deeply reddened sunset falls outside: converted honestly it
    /// comes back with a small negative amount of blue, which is the arithmetic saying "a color this
    /// pure cannot be made from these primaries."
    /// </para>
    /// <para>
    /// A negative cannot simply be left, because these colors light scenes as well as being looked at,
    /// and a light with a negative in it would <i>take</i> that color out of whatever it fell on.  Nor
    /// is clipping it to nothing right, since that quietly changes the hue and loses the light
    /// altogether.  What is done instead is to mix in just enough white to lift the deepest channel to
    /// nothing, and then take back exactly the brightness that added -- so an unshowable color comes
    /// out paler than it truly is, which is the honest failure, rather than darker or a different
    /// color.  It is what a photograph of a sunset does too, and for the same reason.
    /// </para>
    /// </summary>
    /// <param name="color">The color to bring into range.</param>
    /// <returns>The nearest color a screen can show.</returns>
    private static Color IntoGamut(Color color)
    {
        double least = Math.Min(color.Red, Math.Min(color.Green, color.Blue));

        if (least >= 0)
            return color;

        Color paler = new (color.Red - least, color.Green - least, color.Blue - least);
        double was = BrightnessOf(color);
        double now = BrightnessOf(paler);

        if (was <= 0 || now <= 0)
            return new Color(0, 0, 0);

        double back = was / now;

        return new Color(paler.Red * back, paler.Green * back, paler.Blue * back);
    }

    /// <summary>
    /// This method returns how bright a color reads, the three primaries counting for very different
    /// amounts of it.
    /// </summary>
    /// <param name="color">The color in question.</param>
    /// <returns>How bright it reads.</returns>
    private static double BrightnessOf(Color color)
    {
        return 0.2126 * color.Red + 0.7152 * color.Green + 0.0722 * color.Blue;
    }

    /// <summary>
    /// This method does the conversion proper, with no balancing applied: the spectrum weighed
    /// against the matching functions and carried across to a screen's primaries.
    /// <para>
    /// This is what the arithmetic actually says, and it is offered alongside the balanced form so
    /// that the balancing stays a visible choice rather than something buried.  An even spectrum
    /// comes back from here at about <c>(1.20, 0.95, 0.91)</c>, which is the published sRGB reading
    /// of the equal energy illuminant, and is the figure worth checking this against.
    /// </para>
    /// </summary>
    /// <param name="perBand">How much light there is in each band.</param>
    /// <returns>The color that light appears as, before balancing.</returns>
    public static Color ToUnbalancedColor(ReadOnlySpan<double> perBand)
    {
        double x = 0, y = 0, z = 0;

        for (int band = 0; band < Bands; band++)
        {
            double wavelength = WavelengthOf(band);
            double amount = perBand[band];

            x += amount * MatchRed(wavelength);
            y += amount * MatchGreen(wavelength);
            z += amount * MatchBlue(wavelength);
        }

        x /= MatchingTotals.Green;
        y /= MatchingTotals.Green;
        z /= MatchingTotals.Green;

        // The primaries a screen has are not the ones the eye was measured in, so the three sums must
        // be carried across to them.  A color outside what a screen can show comes out negative here;
        // it is left alone rather than clipped, since what to do about that belongs to whatever is
        // writing the picture and not to arithmetic.
        return new Color(
            3.2406 * x - 1.5372 * y - 0.4986 * z,
            -0.9689 * x + 1.8758 * y + 0.0415 * z,
            0.0557 * x - 0.2040 * y + 1.0570 * z);
    }

    /// <summary>
    /// This field holds what an even spectrum has to be multiplied by, color by color, to come out
    /// white.  It is worked out from the same functions and the same bands the conversion itself
    /// uses, so the two cannot drift apart.
    /// </summary>
    private static readonly (double Red, double Green, double Blue) WhiteBalance = WorkOutBalance();

    /// <summary>
    /// This method sums each matching function across the bands.
    /// </summary>
    /// <returns>What each matching function comes to over the whole range.</returns>
    private static (double, double, double) WorkOutTotals()
    {
        double red = 0, green = 0, blue = 0;

        for (int band = 0; band < Bands; band++)
        {
            double wavelength = WavelengthOf(band);

            red += MatchRed(wavelength);
            green += MatchGreen(wavelength);
            blue += MatchBlue(wavelength);
        }

        return (red, green, blue);
    }

    /// <summary>
    /// This method works out what an even spectrum must be multiplied by to come out white.
    /// </summary>
    /// <returns>The three numbers to multiply by.</returns>
    private static (double, double, double) WorkOutBalance()
    {
        double[] even = new double[Bands];

        Array.Fill(even, 1d);

        Color raw = ToUnbalancedColor(even);

        return (1 / raw.Red, 1 / raw.Green, 1 / raw.Blue);
    }

    /// <summary>
    /// These methods give how strongly each of the eye's three answers is stirred by light of a given
    /// wavelength -- the CIE's 1931 color matching functions for a two degree field.
    /// <para>
    /// They are given as sums of lopsided bell curves rather than as the table of measurements they
    /// come from.  The fit is Wyman, Sloan and Shirley's, and it is within a fraction of a percent of
    /// the table everywhere, which is far inside anything a picture could show.  A few lines of
    /// arithmetic are easier to check than four hundred rows of numbers, and they can be asked about
    /// any wavelength rather than the ones somebody happened to tabulate.
    /// </para>
    /// </summary>
    /// <param name="wavelength">The wavelength being asked about, in nanometers.</param>
    /// <returns>How strongly that answer is stirred.</returns>
    private static double MatchRed(double wavelength)
    {
        return 1.056 * Bell(wavelength, 599.8, 37.9, 31.0) +
               0.362 * Bell(wavelength, 442.0, 16.0, 26.7) -
               0.065 * Bell(wavelength, 501.1, 20.4, 26.2);
    }

    /// <inheritdoc cref="MatchRed"/>
    private static double MatchGreen(double wavelength)
    {
        return 0.821 * Bell(wavelength, 568.8, 46.9, 40.5) +
               0.286 * Bell(wavelength, 530.9, 16.3, 31.1);
    }

    /// <inheritdoc cref="MatchRed"/>
    private static double MatchBlue(double wavelength)
    {
        return 1.217 * Bell(wavelength, 437.0, 11.8, 36.0) +
               0.681 * Bell(wavelength, 459.0, 26.0, 13.8);
    }

    /// <summary>
    /// This method gives a bell curve that is allowed to be wider on one side of its peak than the
    /// other, which is what lets so few of them stand in for curves as lopsided as the eye's.
    /// </summary>
    /// <param name="value">Where the curve is being asked about.</param>
    /// <param name="peak">Where the curve is at its tallest.</param>
    /// <param name="spreadBelow">How wide the curve is below its peak.</param>
    /// <param name="spreadAbove">How wide the curve is above its peak.</param>
    /// <returns>The height of the curve there.</returns>
    private static double Bell(double value, double peak, double spreadBelow, double spreadAbove)
    {
        double offset = (value - peak) / (value < peak ? spreadBelow : spreadAbove);

        return Math.Exp(-0.5 * offset * offset);
    }
}
