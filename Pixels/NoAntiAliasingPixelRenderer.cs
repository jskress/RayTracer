using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Pixels;

/// <summary>
/// This class renders a pixel by firing one, and only one, ray through its center and
/// returning the color found.
/// </summary>
public class NoAntiAliasingPixelRenderer : PixelRenderer
{
    public NoAntiAliasingPixelRenderer(PixelToRayConverter converter) : base(converter) {}

    /// <summary>
    /// This method is used to render a specific pixel by determining the appropriate
    /// color and returning it.  The implementation of this method must be stateless
    /// since the same instance is used for all pixels.
    /// </summary>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="x">The X coordinate of the pixel to render.</param>
    /// <param name="y">The Y coordinate of the pixel to render.</param>
    /// <returns>The color for the indicated pixel.</returns>
    public override Color Render(Scene scene, int x, int y)
    {
        Ray ray = Converter.GetRayForPixel(x, y);

        return scene.GetColorFor(ray);
    }
}
