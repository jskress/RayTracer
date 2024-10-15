using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Graphics;

namespace RayTracer.Pixels;

/// <summary>
/// This class renders a pixel by firing five rays for each pixel in a pattern that looks
/// like the 5-pip side of a die.  If any of the four corner colors differ from the center
/// color by more than a threshold, recursion will happen on that corner (up to a certain
/// depth).  Then the five colors are averaged to produce the final color.  
/// </summary>
public class AdaptiveSuperSamplingPixelRenderer : PixelRenderer
{
    private const double DistanceThreshold = 0.1;

    private readonly int _maximumDepth;

    public AdaptiveSuperSamplingPixelRenderer(PixelToRayConverter converter, int maximumDepth)
        : base(converter)
    {
        _maximumDepth = maximumDepth;
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
    public override Color Render(Scene scene, int x, int y)
    {
        return Evaluate(scene, x, y, 0.5, 0.5, 0.25, _maximumDepth);
    }

    /// <summary>
    /// This method is used to evaluate the color for a particular point within a pixel.
    /// It will recurse as necessary.
    /// </summary>
    /// <param name="scene">The scene being rendered.</param>
    /// <param name="x">The X coordinate of the pixel to render.</param>
    /// <param name="y">The Y coordinate of the pixel to render.</param>
    /// <param name="cx">The X coordinate of what to take as the center of our five points.</param>
    /// <param name="cy">The Y coordinate of what to take as the center of our five points.</param>
    /// <param name="shift">The amount of shift to get the four surrounding points.</param>
    /// <param name="depth">The current recursion depth, aiming toward 0.</param>
    /// <returns>The color for the given point within the pixel.</returns>
    private Color Evaluate(Scene scene, int x, int y, double cx, double cy, double shift, int depth)
    {
        Color center = scene.GetColorFor(
            Converter.GetRayForPixel(x, y, cx, cy));
        Color topLeft = scene.GetColorFor(
            Converter.GetRayForPixel(x, y, cx, cy, -shift, -shift));
        Color topRight = scene.GetColorFor(
            Converter.GetRayForPixel(x, y, cx, cy, shift, -shift));
        Color bottomLeft = scene.GetColorFor(
            Converter.GetRayForPixel(x, y, cx, cy, -shift, shift));
        Color bottomRight = scene.GetColorFor(
            Converter.GetRayForPixel(x, y, cx, cy, shift, shift));

        if (depth > 0)
        {
            double nextShift = shift / 2;

            depth--;

            if (center.Distance(topLeft) > DistanceThreshold)
                topLeft = Evaluate(scene, x, y, cx - shift, cy - shift, nextShift, depth);

            if (center.Distance(topRight) > DistanceThreshold)
                topRight = Evaluate(scene, x, y, cx + shift, cy - shift, nextShift, depth);

            if (center.Distance(bottomLeft) > DistanceThreshold)
                bottomLeft = Evaluate(scene, x, y, cx - shift, cy + shift, nextShift, depth);

            if (center.Distance(bottomRight) > DistanceThreshold)
                bottomRight = Evaluate(scene, x, y, cx + shift, cy + shift, nextShift, depth);
        }

        return Colors.Average([center, topLeft, topRight, bottomLeft, bottomRight]);
    }
}
