namespace RayTracer.Graphics;

/// <summary>
/// This interface describes the light a render carries about as it shades: what arrives at a point,
/// what a surface sends on, what gets through a pane of glass or a bank of fog.
/// <para>
/// **It is not a color, and is kept apart from <see cref="Color"/> on purpose.**  A color is what an
/// author writes -- a pigment's red, a lamp's warm white -- and stays in red, green and blue whatever
/// the render does.  Light is what the render works in, and how it is held is the render's choice:
/// three broad bands, which is what this renderer has always done and is still the default, or many
/// narrow ones, which is what it takes for light to split in a prism or a sunset to redden as it
/// truly does.  Everything an author wrote is turned into light where the shading first meets it,
/// and light is turned back into a color once, when a pixel is done.
/// </para>
/// <para>
/// **Turning a color into light depends on what the color describes**, which is why there are three
/// ways to do it.  A surface's color says what share of each part of the light it gives back, and can
/// never give back more than it was given.  A lamp's color says what light it gives off, and white
/// must come back out as white.  A medium's colors are rates -- how much is swallowed or turned aside
/// per unit of distance -- which have no upper bound at all.  Held in three bands these are all the
/// same thing, the numbers as written; held in many, each is a different question.
/// </para>
/// <para>
/// **The shading is written once, over any kind of light**, rather than once per kind.  Each method that
/// carries light is generic in it, constrained to a struct, so that the runtime compiles a copy of it
/// for each kind with every operation below inlined: three bands cost what three numbers cost, with
/// nothing to look up and nothing allocated.
/// </para>
/// <para>
/// **The fourth number is how much of the pixel is covered**, and follows <see cref="Color"/>'s rule
/// exactly: every operator leaves it at one, and only the few places that compose it say otherwise.
/// It only ever survives to a pixel when a ray saw nothing but the background, or saw it through
/// fog.
/// </para>
/// </summary>
/// <typeparam name="TSelf">The kind of light itself.</typeparam>
public interface ISpectrum<TSelf> where TSelf : struct, ISpectrum<TSelf>
{
    /// <summary>
    /// This property holds how many bands this kind of light is held in.
    /// </summary>
    static abstract int Count { get; }

    /// <summary>
    /// This property reports whether this kind of light is held wavelength by wavelength, rather than in
    /// the three broad bands that are simply a color's channels.  Where something was worked out
    /// wavelength by wavelength in the first place -- the sky -- it is handed over as it was worked out
    /// only to a kind that can hold it, and as the color it has always been to the rest.
    /// </summary>
    static abstract bool IsSpectral { get; }

    /// <summary>
    /// This property holds the one wavelength, in nanometers, this kind of light is carried at, or
    /// <c>NaN</c> for light that spans them all.  Only a ray split off to carry a single band has one,
    /// and it is what a substance that spreads colors bends that ray by.
    /// </summary>
    static abstract double Wavelength { get; }

    /// <summary>
    /// This property holds no light at all, fully covering its pixel.
    /// </summary>
    static abstract TSelf Black { get; }

    /// <summary>
    /// This property holds light of one in every band, fully covering its pixel: all of a lamp's
    /// light getting through, or a tint that changes nothing.
    /// </summary>
    static abstract TSelf White { get; }

    /// <summary>
    /// This method turns a surface's color into the share of each band it gives back.
    /// </summary>
    /// <param name="color">The surface's color.</param>
    /// <returns>The share it gives back, covering as much as the color does.</returns>
    static abstract TSelf FromReflectance(Color color);

    /// <summary>
    /// This method turns the color of something that gives off light -- a lamp, the sky, a glowing
    /// fog -- into the light it gives off.
    /// </summary>
    /// <param name="color">The color of the light.</param>
    /// <returns>The light, covering as much as the color does.</returns>
    static abstract TSelf FromIlluminant(Color color);

    /// <summary>
    /// This method turns a rate written as a color -- how much a medium swallows or turns aside per
    /// unit of distance -- into the rate in each band.
    /// </summary>
    /// <param name="color">The rate, as written.</param>
    /// <returns>The rate in each band.</returns>
    static abstract TSelf FromUnbounded(Color color);

    /// <summary>
    /// This method takes light that was worked out wavelength by wavelength, in
    /// <see cref="SpectralColor"/>'s bands.
    /// </summary>
    /// <param name="perBand">The light, one amount for each of <see cref="SpectralColor"/>'s bands.</param>
    /// <returns>The light, fully covering its pixel.</returns>
    static abstract TSelf FromSampled(ReadOnlySpan<double> perBand);

    /// <summary>
    /// This property holds the amount in one band.
    /// </summary>
    /// <param name="band">Which band, from nought up to <see cref="Count"/>.</param>
    double this[int band] { get; set; }

    /// <summary>
    /// This property holds how much of its pixel this light covers.
    /// </summary>
    double Alpha { get; }

    /// <summary>
    /// This method returns a copy of this light covering the given amount of its pixel.
    /// </summary>
    /// <param name="alpha">How much of the pixel the copy covers.</param>
    /// <returns>The copy.</returns>
    TSelf WithAlpha(double alpha);

    /// <summary>
    /// This property reports whether this is no light at all, to within the renderer's tolerance, and
    /// fully covers its pixel -- which is what matching <see cref="Colors.Black"/> has always meant.
    /// </summary>
    bool IsBlack { get; }

    /// <summary>
    /// This property holds the amounts in every band, added up.
    /// </summary>
    double Sum { get; }

    /// <summary>
    /// This property holds the amount in the average band.
    /// </summary>
    double Average { get; }

    /// <summary>
    /// This method turns this light into the color a pixel shows of it.
    /// </summary>
    /// <returns>The color.</returns>
    Color ToColor();

    static abstract TSelf operator +(TSelf left, TSelf right);
    static abstract TSelf operator -(TSelf left, TSelf right);
    static abstract TSelf operator *(TSelf left, TSelf right);
    static abstract TSelf operator *(TSelf left, double right);
    static abstract TSelf operator *(double left, TSelf right);
    static abstract TSelf operator /(TSelf left, double right);
}
