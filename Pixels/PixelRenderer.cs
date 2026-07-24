
using RayTracer.Core;
using RayTracer.Graphics;

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
        int count = Converter.Lens.SampleCount;

        if (count == 1)
            return scene.GetColorFor(Converter.GetRayForPixel(x, y, centerX, centerY, shiftX, shiftY));

        Color sum = Colors.Black;

        for (int index = 0; index < count; index++)
        {
            sum += scene.GetColorFor(
                Converter.GetRayForPixel(x, y, centerX, centerY, shiftX, shiftY, index));
        }

        return sum * (1.0 / count);
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
