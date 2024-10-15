
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
