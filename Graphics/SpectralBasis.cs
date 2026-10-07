namespace RayTracer.Graphics;

/// <summary>
/// This class holds what it takes to work in bands: where they lie, how light held in them is turned
/// into a color, and how a color is turned into light held in them.
/// <para>
/// **The bands are the sky's**: <see cref="SpectralColor.Bands"/> of them, sharing the range from
/// <see cref="SpectralColor.ShortestWavelength"/> to <see cref="SpectralColor.LongestWavelength"/>
/// evenly, so that a spectrum the sky works out is already in them.
/// </para>
/// <para>
/// **Light is turned into a color by weighing each band against the eye's matching functions** averaged
/// over the band rather than read at its middle, since they change a good deal across even twelve and a
/// half nanometers.  The result is balanced the way <see cref="SpectralColor"/>
/// balances, so that light of the same strength in every band is white -- which is what lets a white
/// light be a white light whichever way the render carries it, and is why nothing here is multiplied
/// by a daylight spectrum.
/// </para>
/// <para>
/// **A color is turned into light by <see cref="RgbToSpectrumTable"/>**, fitted against exactly this
/// conversion, so that a color goes in and comes back out as itself.
/// </para>
/// </summary>
public class SpectralBasis
{
    /// <summary>
    /// This property holds how many bands there are.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// This property holds where the middle of each band falls across the visible range, from nought at
    /// the short end to one at the long.
    /// </summary>
    public double[] Positions { get; }

    /// <summary>
    /// This property holds the conversion from bands to color: three rows, red, green and blue, of one
    /// weight per band, row after row.  It is linear, balanced, and has not yet been brought into gamut.
    /// </summary>
    public double[] Forward { get; }

    /// <summary>
    /// This property holds the table that turns a color into a curve, built the first time it is
    /// wanted, since building it is the one costly thing here.
    /// </summary>
    public RgbToSpectrumTable Table => _table.Value;

    /// <summary>
    /// This property holds the one basis there is, made the first time it is asked for.
    /// </summary>
    public static SpectralBasis Shared => Lazily.Value;

    private static readonly Lazy<SpectralBasis> Lazily = new (() => new SpectralBasis(SpectralColor.Bands));

    private readonly Lazy<RgbToSpectrumTable> _table;
    private readonly double[] _correction;

    /// <summary>
    /// This constructor lays out the given number of bands across the visible range.  The renderer
    /// uses <see cref="Shared"/>; other counts are for measuring what a different choice would do.
    /// </summary>
    /// <param name="count">How many bands.</param>
    public SpectralBasis(int count)
    {
        Count = count;
        Positions = new double[count];
        Forward = new double[3 * count];

        double width = (SpectralColor.LongestWavelength - SpectralColor.ShortestWavelength) / count;
        double[] x = new double[count];
        double[] y = new double[count];
        double[] z = new double[count];
        double brightness = 0;

        for (int band = 0; band < count; band++)
        {
            double start = SpectralColor.ShortestWavelength + band * width;

            Positions[band] = (band + 0.5) / count;

            // The matching functions averaged across the band, by the midpoint rule at a quarter of a
            // nanometer or finer.
            int steps = Math.Max(1, (int) Math.Ceiling(width * 4));

            for (int step = 0; step < steps; step++)
            {
                double wavelength = start + (step + 0.5) * width / steps;

                x[band] += SpectralColor.MatchRed(wavelength) / steps;
                y[band] += SpectralColor.MatchGreen(wavelength) / steps;
                z[band] += SpectralColor.MatchBlue(wavelength) / steps;
            }

            brightness += y[band];
        }

        // XYZ to linear sRGB, the same matrix SpectralColor uses, then the balance that makes an even
        // spectrum white.
        double[,] toRgb =
        {
            { 3.2406, -1.5372, -0.4986 },
            { -0.9689, 1.8758, 0.0415 },
            { 0.0557, -0.2040, 1.0570 }
        };

        for (int row = 0; row < 3; row++)
        {
            double even = 0;

            for (int band = 0; band < count; band++)
            {
                double weight = (toRgb[row, 0] * x[band] + toRgb[row, 1] * y[band] +
                                 toRgb[row, 2] * z[band]) / brightness;

                Forward[row * count + band] = weight;
                even += weight;
            }

            for (int band = 0; band < count; band++)
                Forward[row * count + band] /= even;
        }

        _correction = SmallestSpectraFor(Forward, count);
        _table = new Lazy<RgbToSpectrumTable>(() => new RgbToSpectrumTable(Forward, Positions));
    }

    /// <summary>
    /// This method makes sure the table is built, so that a render can pay for it before its pixels
    /// start rather than in the middle of them.
    /// </summary>
    public void Prepare()
    {
        _ = _table.Value;
    }

    /// <summary>
    /// This method turns light held in these bands into the color it is seen as.
    /// </summary>
    /// <param name="bands">The light, band by band.</param>
    /// <param name="alpha">How much of its pixel it covers.</param>
    /// <returns>The color.</returns>
    public Color ToColor(ReadOnlySpan<double> bands, double alpha)
    {
        double red = 0, green = 0, blue = 0;

        for (int band = 0; band < Count; band++)
        {
            red += Forward[band] * bands[band];
            green += Forward[Count + band] * bands[band];
            blue += Forward[2 * Count + band] * bands[band];
        }

        // A pure enough spectrum -- light split by glass, say -- can be a color no screen has, and
        // comes out with a negative in it; it is brought in the way the sky's colors are.
        Color color = SpectralColor.IntoGamut(new Color(red, green, blue));

        return alpha == 1 ? color : color.WithAlpha(alpha);
    }

    /// <summary>
    /// This method turns a color into light held in these bands.
    /// <para>
    /// A surface's color is a share of the light that falls on it and can never be more than all of it,
    /// so while its channels stay within one it is given the table's own curve, which stays between
    /// nought and one as a reflectance must.  Anything else -- a light, a rate, or a surface said to
    /// give back more than it is given -- has no ceiling, and is fitted at half its largest channel and
    /// scaled back up, pbrt's way.  Doing that to every light keeps a light's spectrum the same shape
    /// however bright it is said to be, which a dimmer switch should.
    /// </para>
    /// </summary>
    /// <param name="color">The color.  A negative channel is taken as nothing, a negative amount of
    /// light having no spectrum.</param>
    /// <param name="reflectance">Whether the color is a surface's share of light.</param>
    /// <param name="into">Where to put the light, band by band.</param>
    public void Uplift(Color color, bool reflectance, Span<double> into)
    {
        double red = Math.Max(0, color.Red);
        double green = Math.Max(0, color.Green);
        double blue = Math.Max(0, color.Blue);
        double largest = Math.Max(red, Math.Max(green, blue));

        if (largest <= 0)
        {
            into[..Count].Clear();
            return;
        }

        bool bounded = reflectance && largest <= 1;
        double scale = bounded ? 1 : 2 * largest;
        (double, double, double) curve = Table.CoefficientsFor(red / scale, green / scale, blue / scale);
        double backRed = 0, backGreen = 0, backBlue = 0;

        for (int band = 0; band < Count; band++)
        {
            double amount = scale * RgbToSpectrumTable.Evaluate(curve, Positions[band]);

            into[band] = amount;
            backRed += Forward[band] * amount;
            backGreen += Forward[Count + band] * amount;
            backBlue += Forward[2 * Count + band] * amount;
        }

        // The table's coefficients are interpolated, so its curve comes back a little off the color --
        // by a level of eight bits for about one color in ten, which is enough to see in a comparison if
        // not by eye.  The conversion back is linear, so the miss is made up exactly by adding the
        // smallest spectrum that makes it up; being small, it leaves the curve as smooth as it was.  A
        // color no curve can reach -- a few of the most saturated -- is brought as near as staying a
        // possible spectrum allows: never negative, and never more than all the light for a surface.
        double missedRed = red - backRed;
        double missedGreen = green - backGreen;
        double missedBlue = blue - backBlue;

        for (int band = 0; band < Count; band++)
        {
            double amount = into[band] +
                            _correction[3 * band] * missedRed +
                            _correction[3 * band + 1] * missedGreen +
                            _correction[3 * band + 2] * missedBlue;

            into[band] = bounded ? Math.Clamp(amount, 0, 1) : Math.Max(0, amount);
        }
    }

    /// <summary>
    /// This method works out, for each of red, green and blue, the smallest spectrum that converts to
    /// one of it and none of the others: the conversion's pseudo-inverse, Fᵀ(FFᵀ)⁻¹.
    /// </summary>
    /// <param name="forward">The conversion from bands to color.</param>
    /// <param name="count">How many bands.</param>
    /// <returns>The three spectra, band after band, three numbers to a band.</returns>
    private static double[] SmallestSpectraFor(double[] forward, int count)
    {
        double[,] product = new double[3, 3];

        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                for (int band = 0; band < count; band++)
                    product[row, column] += forward[row * count + band] * forward[column * count + band];
            }
        }

        double[,] inverse = Inverse(product);
        double[] spectra = new double[3 * count];

        for (int band = 0; band < count; band++)
        {
            for (int column = 0; column < 3; column++)
            {
                for (int row = 0; row < 3; row++)
                    spectra[3 * band + column] += forward[row * count + band] * inverse[row, column];
            }
        }

        return spectra;
    }

    /// <summary>
    /// This method inverts a three by three matrix, by its adjugate.
    /// </summary>
    private static double[,] Inverse(double[,] m)
    {
        double a = m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1];
        double b = m[1, 2] * m[2, 0] - m[1, 0] * m[2, 2];
        double c = m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0];
        double determinant = m[0, 0] * a + m[0, 1] * b + m[0, 2] * c;

        return new double[,]
        {
            {
                a / determinant, (m[0, 2] * m[2, 1] - m[0, 1] * m[2, 2]) / determinant,
                (m[0, 1] * m[1, 2] - m[0, 2] * m[1, 1]) / determinant
            },
            {
                b / determinant, (m[0, 0] * m[2, 2] - m[0, 2] * m[2, 0]) / determinant,
                (m[0, 2] * m[1, 0] - m[0, 0] * m[1, 2]) / determinant
            },
            {
                c / determinant, (m[0, 1] * m[2, 0] - m[0, 0] * m[2, 1]) / determinant,
                (m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0]) / determinant
            }
        };
    }
}
