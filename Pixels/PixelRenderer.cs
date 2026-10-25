
using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

using RayTracer.Renderer;

namespace RayTracer.Pixels;

/// <summary>
/// This class is the base class for all types of pixel renders.  This is mostly used to
/// support antialiasing.
/// </summary>
public abstract class PixelRenderer
{
    /// <summary>
    /// This property holds the object to use to convert pixels to rays
    /// </summary>
    protected PixelToRayConverter Converter { get; }

    /// <summary>
    /// This property holds where to put the counts, or null when nobody asked for any.  It is set
    /// once before the render starts rather than passed down through every call, since the same
    /// renderer instance serves every pixel on every thread.
    /// </summary>
    internal Statistics Statistics { get; set; }

    /// <summary>
    /// This property notes whether light is carried wavelength by wavelength rather than as red, green
    /// and blue.  It is set once before the render starts, as the statistics are.
    /// </summary>
    internal bool Spectral { get; set; }

    /// <summary>
    /// This property holds what a picture will show of the light a sample brings, for telling samples apart
    /// by, or null to tell them apart by the light itself.  Exposed brighter, two samples a sampler took for
    /// alike in the light may be far apart in the picture; eased toward white, two far apart may show alike.
    /// It is set once before the render starts, as the statistics are.
    /// </summary>
    internal Func<Color, Color> Seen { get; set; }

    protected PixelRenderer(PixelToRayConverter converter)
    {
        Converter = converter;
    }

    /// <summary>
    /// This method works out the color seen at one spot within a pixel, gathering it across the
    /// camera's lens.
    /// <para>
    /// A pinhole camera is kept apart and left exactly as it always was: one ray, no averaging and
    /// no arithmetic laid over the color it comes back with, so every picture rendered before any
    /// of this existed renders the same to the bit.  A camera with width to its lens fires from
    /// several places across it and averages, which is what puts the near and the far out of focus.
    /// </para>
    /// <para>
    /// Gathering the lens here, at one spot within the pixel, rather than out where the pixel's own
    /// samples are compared, is what keeps this from fighting the anti-aliasing: the color handed
    /// back has already settled, so the supersampler is comparing colors rather than the scatter of
    /// the lens, and only splits a pixel further where the picture really does change across it.
    /// </para>
    /// </summary>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="x">The X coordinate of the pixel to render.</param>
    /// <param name="y">The Y coordinate of the pixel to render.</param>
    /// <param name="centerX">The X offset within the pixel to treat as center.</param>
    /// <param name="centerY">The Y offset within the pixel to treat as center.</param>
    /// <param name="shiftX">The amount to shift the X coordinate off center of the pixel.</param>
    /// <param name="shiftY">The amount to shift the Y coordinate off center of the pixel.</param>
    /// <returns>The color seen at that spot within the pixel.</returns>
    protected Color Trace(
        Scene scene, int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0)
    {
        return Spectral
            ? Trace<BandSpectrum>(scene, x, y, centerX, centerY, shiftX, shiftY)
            : Trace<RgbSpectrum>(scene, x, y, centerX, centerY, shiftX, shiftY);
    }

    /// <summary>
    /// This method works out the color seen at one spot within a pixel, carrying the light in the
    /// given kind of bands and turning it into a color only once the lens has been gathered.
    /// </summary>
    /// <typeparam name="TS">The kind of light to carry.</typeparam>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="x">The X coordinate of the pixel to render.</param>
    /// <param name="y">The Y coordinate of the pixel to render.</param>
    /// <param name="centerX">The X offset within the pixel to treat as center.</param>
    /// <param name="centerY">The Y offset within the pixel to treat as center.</param>
    /// <param name="shiftX">The amount to shift the X coordinate off center of the pixel.</param>
    /// <param name="shiftY">The amount to shift the Y coordinate off center of the pixel.</param>
    /// <returns>The color seen at that spot within the pixel.</returns>
    private Color Trace<TS>(
        Scene scene, int x, int y, double centerX, double centerY, double shiftX, double shiftY)
        where TS : struct, ISpectrum<TS>
    {
        int count = Converter.Sampler.SampleCount;

        Statistics?.CountSample(count);

        if (count == 1)
        {
            Ray ray = Converter.GetRayForPixel(x, y, centerX, centerY, shiftX, shiftY);

            // What this thread is shading is this scene, at this ray's instant, from the start: until a hit set it,
            // a pattern looking about from a shadow ray could look about the last frame's scene.
            scene.ShadeHere(ray.TimeIndex);

            return scene.GetColorFor<TS>(ray, scene.TraceDepth).ToColor();
        }

        TS sum = TS.Black;
        double alpha = 0;

        // Light adds, and coverage averages: summed as light, every pixel came out opaque, so a camera with a lens
        // or an open shutter filled the transparent background and lost the soft edge of what stood against it.
        for (int index = 0; index < count; index++)
        {
            Ray ray = Converter.GetRayForPixel(x, y, centerX, centerY, shiftX, shiftY, index);

            scene.ShadeHere(ray.TimeIndex);

            TS seen = scene.GetColorFor<TS>(ray, scene.TraceDepth);

            sum += seen;
            alpha += seen.Alpha;
        }

        return (sum * (1.0 / count)).WithAlpha(alpha / count).ToColor();
    }

    /// <summary>
    /// This method is used to render a specific pixel by determining the appropriate
    /// color and returning it.  The implementation of this method must be stateless
    /// since the same instance is used for all pixels.
    /// </summary>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="x">The X coordinate of the pixel to render.</param>
    /// <param name="y">The Y coordinate of the pixel to render.</param>
    /// <returns>The color for the indicated pixel.</returns>
    public abstract Color Render(Scene scene, int x, int y);
}
