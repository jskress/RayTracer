using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This struct holds light in just one of <see cref="BandSpectrum"/>'s bands, for a ray that carries
/// that band alone.
/// <para>
/// **Such rays exist because of glass that spreads colors.**  Where a ray carrying every band crosses
/// into it, each band bends by its own amount and goes its own way, so the ray is split into one ray
/// per band.  Each carries its band from there on, through the rest of its path, and never splits again
/// -- it has only the one band left to send anywhere.  Carrying that one band as a whole spectrum would
/// cost a full spectrum's arithmetic thirty-two times over for nothing, which is what this is for.
/// </para>
/// <para>
/// **Which band is said once, for the thread, by whatever splits the ray** -- see <see cref="Carry"/> --
/// rather than carried in every value, since turning a color into light has to know it and is not
/// handed anything but the color.  A split happens and is finished within one call on one thread, and
/// nothing inside it splits again, so there is only ever the one band to know about.  The exact
/// wavelength within the band is said alongside, since it is what decides how far glass bends the ray.
/// </para>
/// </summary>
public struct SingleBand : ISpectrum<SingleBand>
{
    [ThreadStatic]
    private static int _band;

    [ThreadStatic]
    private static double _wavelength;

    /// <summary>
    /// This property holds which of <see cref="BandSpectrum"/>'s bands the light on this thread is
    /// carried in.
    /// </summary>
    public static int Band => _band;

    /// <summary>
    /// This method says which band, and which wavelength within it, the light on this thread is
    /// carried at, and returns what was said before, so that whatever says it can put it back.
    /// </summary>
    /// <param name="band">Which of <see cref="BandSpectrum"/>'s bands.</param>
    /// <param name="wavelength">The wavelength, in nanometers, somewhere within that band.</param>
    /// <returns>The band and wavelength said before.</returns>
    public static (int Band, double Wavelength) Carry(int band, double wavelength)
    {
        (int, double) was = (_band, _wavelength);

        _band = band;
        _wavelength = wavelength;

        return was;
    }

    /// <summary>
    /// This property holds the amount of light.
    /// </summary>
    public double Amount { get; private set; }

    /// <summary>
    /// This property holds how much of its pixel this light covers.
    /// </summary>
    public double Alpha { get; private set; }

    public static int Count => 1;
    public static bool IsSpectral => true;
    public static double Wavelength => _wavelength;
    public static SingleBand Black => new (0);
    public static SingleBand White => new (1);

    public SingleBand(double amount, double alpha = 1)
    {
        Amount = amount;
        Alpha = alpha;
    }

    public static SingleBand FromReflectance(Color color) =>
        new (BandSpectrum.FromReflectance(color)[_band], color.Alpha);

    public static SingleBand FromIlluminant(Color color) =>
        new (BandSpectrum.FromIlluminant(color)[_band], color.Alpha);

    public static SingleBand FromUnbounded(Color color) =>
        new (BandSpectrum.FromUnbounded(color)[_band], color.Alpha);

    public static SingleBand FromSampled(ReadOnlySpan<double> perBand) => new (perBand[_band]);

    public double this[int band]
    {
        readonly get => band == 0 ? Amount : throw new ArgumentOutOfRangeException(nameof(band));
        set
        {
            if (band != 0)
                throw new ArgumentOutOfRangeException(nameof(band));

            Amount = value;
        }
    }

    public readonly SingleBand WithAlpha(double alpha) => new (Amount, alpha);

    public readonly bool IsBlack => Amount.Near(0) && Alpha.Near(1);

    public readonly double Sum => Amount;

    public readonly double Average => Amount;

    /// <summary>
    /// This method returns the color of this light seen on its own: a pure spectral color, as near as
    /// a screen can show it.  A render never shows one of these directly, its band being gathered back
    /// in with the others first, so this is for looking at.
    /// </summary>
    /// <returns>The color.</returns>
    public readonly Color ToColor()
    {
        Span<double> bands = stackalloc double[SpectralColor.Bands];

        bands.Clear();
        bands[_band] = Amount;

        return SpectralBasis.Shared.ToColor(bands, Alpha);
    }

    public override readonly string ToString()
    {
        return $"Band {_band}: {Amount}{(Alpha.Near(1) ? "" : $"; {Alpha}")}";
    }

    public static SingleBand operator +(SingleBand left, SingleBand right) => new (left.Amount + right.Amount);
    public static SingleBand operator -(SingleBand left, SingleBand right) => new (left.Amount - right.Amount);
    public static SingleBand operator *(SingleBand left, SingleBand right) => new (left.Amount * right.Amount);
    public static SingleBand operator *(SingleBand left, double right) => new (left.Amount * right);
    public static SingleBand operator *(double left, SingleBand right) => new (left * right.Amount);
    public static SingleBand operator /(SingleBand left, double right) => new (left.Amount / right);
}
