using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This struct holds light in narrow bands across the visible range, which is what lets a render follow
/// what light does differently at different wavelengths: a yellow light on a blue paint, a prism, a
/// sunset.
/// <para>
/// **There are thirty-two bands, twelve and a half nanometers each**, the sky's own.  That was settled by
/// rendering the gallery at eight, sixteen and thirty-two.  Under white light a spectral render should
/// look exactly like one in red, green and blue, and at thirty-two every scene did, but for the one whose
/// point is colored light through colored glass.  At sixteen, fifteen scenes drifted, every one of them
/// for the same reason: sRGB's purest red is a color no surface can have when it is seen in bands that
/// wide -- not a shortcoming of the fitting, but of the bands, as a linear program over every possible
/// reflectance confirms -- and a scene's <c>Red</c> came out pink.  Sixteen saved a third of the extra
/// time in thick media and next to none anywhere else.
/// </para>
/// <para>
/// **The bands are held inline**, so that light is copied about as plain numbers and never allocated.
/// </para>
/// <para>
/// **Turning a color into light costs a table lookup and a curve evaluated at every band**, which is
/// cheap but not free, and most colors a render meets it meets over and over: a lamp's color, a fog's
/// rates, a plain paint.  A color is a value that never changes once made, so the light it turns into
/// is kept against the color itself, in a small table for each thread, and the same color asked again
/// is answered from there.  It is what keeps a medium's rates, read at every step through it, from being
/// worked out afresh each time.
/// </para>
/// </summary>
public struct BandSpectrum : ISpectrum<BandSpectrum>
{
    private const int CacheSize = 256;

    private static SpectralBasis Basis => SpectralBasis.Shared;

    [ThreadStatic]
    private static Remembered[] _remembered;

    private Storage _bands;

    /// <summary>
    /// This property holds how much of its pixel this light covers.
    /// </summary>
    public double Alpha { get; private set; }

    public static int Count => SpectralColor.Bands;
    public static bool IsSpectral => true;
    public static BandSpectrum Black => Filled(0);
    public static BandSpectrum White => Filled(1);

    public static BandSpectrum FromReflectance(Color color) => Converted(color, true);
    public static BandSpectrum FromIlluminant(Color color) => Converted(color, false);
    public static BandSpectrum FromUnbounded(Color color) => Converted(color, false);

    public static BandSpectrum FromSampled(ReadOnlySpan<double> perBand)
    {
        BandSpectrum light = new () { Alpha = 1 };

        perBand[..SpectralColor.Bands].CopyTo(light.Bands);

        return light;
    }

    public double this[int band]
    {
        readonly get => ReadBands[band];
        set => Bands[band] = value;
    }

    public readonly BandSpectrum WithAlpha(double alpha)
    {
        BandSpectrum copy = this;

        copy.Alpha = alpha;

        return copy;
    }

    public readonly bool IsBlack
    {
        get
        {
            foreach (double amount in ReadBands)
            {
                if (!amount.Near(0))
                    return false;
            }

            return Alpha.Near(1);
        }
    }

    public readonly double Sum
    {
        get
        {
            double sum = 0;

            foreach (double amount in ReadBands)
                sum += amount;

            return sum;
        }
    }

    public readonly double Average => Sum / Count;

    public readonly Color ToColor() => Basis.ToColor(ReadBands, Alpha);

    public override readonly string ToString()
    {
        return $"Bands({string.Join(", ", ReadBands.ToArray())}{(Alpha.Near(1) ? "" : $"; {Alpha}")})";
    }

    public static BandSpectrum operator +(BandSpectrum left, BandSpectrum right)
    {
        BandSpectrum result = new () { Alpha = 1 };
        Span<Vector<double>> into = MemoryMarshal.Cast<double, Vector<double>>(result.Bands);
        ReadOnlySpan<Vector<double>> one = MemoryMarshal.Cast<double, Vector<double>>(left.ReadBands);
        ReadOnlySpan<Vector<double>> other = MemoryMarshal.Cast<double, Vector<double>>(right.ReadBands);

        for (int index = 0; index < into.Length; index++)
            into[index] = one[index] + other[index];

        return result;
    }

    public static BandSpectrum operator -(BandSpectrum left, BandSpectrum right)
    {
        BandSpectrum result = new () { Alpha = 1 };
        Span<Vector<double>> into = MemoryMarshal.Cast<double, Vector<double>>(result.Bands);
        ReadOnlySpan<Vector<double>> one = MemoryMarshal.Cast<double, Vector<double>>(left.ReadBands);
        ReadOnlySpan<Vector<double>> other = MemoryMarshal.Cast<double, Vector<double>>(right.ReadBands);

        for (int index = 0; index < into.Length; index++)
            into[index] = one[index] - other[index];

        return result;
    }

    public static BandSpectrum operator *(BandSpectrum left, BandSpectrum right)
    {
        BandSpectrum result = new () { Alpha = 1 };
        Span<Vector<double>> into = MemoryMarshal.Cast<double, Vector<double>>(result.Bands);
        ReadOnlySpan<Vector<double>> one = MemoryMarshal.Cast<double, Vector<double>>(left.ReadBands);
        ReadOnlySpan<Vector<double>> other = MemoryMarshal.Cast<double, Vector<double>>(right.ReadBands);

        for (int index = 0; index < into.Length; index++)
            into[index] = one[index] * other[index];

        return result;
    }

    public static BandSpectrum operator *(BandSpectrum left, double right)
    {
        BandSpectrum result = new () { Alpha = 1 };
        Span<Vector<double>> into = MemoryMarshal.Cast<double, Vector<double>>(result.Bands);
        ReadOnlySpan<Vector<double>> one = MemoryMarshal.Cast<double, Vector<double>>(left.ReadBands);

        for (int index = 0; index < into.Length; index++)
            into[index] = one[index] * right;

        return result;
    }

    public static BandSpectrum operator *(double left, BandSpectrum right)
    {
        BandSpectrum result = new () { Alpha = 1 };
        Span<Vector<double>> into = MemoryMarshal.Cast<double, Vector<double>>(result.Bands);
        ReadOnlySpan<Vector<double>> other = MemoryMarshal.Cast<double, Vector<double>>(right.ReadBands);

        for (int index = 0; index < into.Length; index++)
            into[index] = left * other[index];

        return result;
    }

    public static BandSpectrum operator /(BandSpectrum left, double right)
    {
        BandSpectrum result = new () { Alpha = 1 };
        Span<Vector<double>> into = MemoryMarshal.Cast<double, Vector<double>>(result.Bands);
        ReadOnlySpan<Vector<double>> one = MemoryMarshal.Cast<double, Vector<double>>(left.ReadBands);

        for (int index = 0; index < into.Length; index++)
            into[index] = one[index] / right;

        return result;
    }

    /// <summary>
    /// This property holds the bands, to be written.
    /// </summary>
    [UnscopedRef]
    private Span<double> Bands =>
        MemoryMarshal.CreateSpan(ref Unsafe.As<Storage, double>(ref _bands), SpectralColor.Bands);

    /// <summary>
    /// This property holds the bands, to be read.
    /// </summary>
    [UnscopedRef]
    private readonly ReadOnlySpan<double> ReadBands =>
        MemoryMarshal.CreateReadOnlySpan(
            ref Unsafe.As<Storage, double>(ref Unsafe.AsRef(in _bands)), SpectralColor.Bands);

    /// <summary>
    /// This method returns light of the same amount in every band, covering its pixel.
    /// </summary>
    private static BandSpectrum Filled(double amount)
    {
        BandSpectrum light = new () { Alpha = 1 };

        light.Bands.Fill(amount);

        return light;
    }

    /// <summary>
    /// This method turns a color into light, from this thread's table of colors already turned if it
    /// can.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="reflectance">Whether it is a surface's share of light, rather than a light or a
    /// rate.</param>
    /// <returns>The light, covering as much as the color does.</returns>
    private static BandSpectrum Converted(Color color, bool reflectance)
    {
        Remembered[] remembered = _remembered ??= new Remembered[CacheSize];
        int slot = (RuntimeHelpers.GetHashCode(color) * 2 + (reflectance ? 1 : 0)) & (CacheSize - 1);
        ref Remembered entry = ref remembered[slot];

        if (ReferenceEquals(entry.Color, color) && entry.Reflectance == reflectance)
            return entry.Light;

        BandSpectrum light = new () { Alpha = color.Alpha };

        Basis.Uplift(color, reflectance, light.Bands);

        entry = new Remembered(color, reflectance, light);

        return light;
    }

    /// <summary>
    /// This record holds one color already turned into light.
    /// </summary>
    private record struct Remembered(Color Color, bool Reflectance, BandSpectrum Light);

    /// <summary>
    /// This struct holds the bands themselves, inline.
    /// </summary>
    [InlineArray(SpectralColor.Bands)]
    private struct Storage
    {
        private double _band;
    }
}
