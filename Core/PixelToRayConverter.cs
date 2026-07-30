using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class is the base for the things that know how a camera turns a pixel into the ray that
/// finds its color.  Each camera projection -- perspective, orthographic, fisheye and the rest --
/// is a subclass that maps a pixel to a ray its own way.  What they share is the view transform
/// that carries a ray from the camera's own space out into the world, and the sampler that spreads
/// the rays of a lens and a shutter.
/// </summary>
public abstract class PixelToRayConverter
{
    /// <summary>
    /// This property holds the transform to use.
    /// </summary>
    public Matrix Transform
    {
        get => _transform;
        set
        {
            _transform = value;
            _inverseTransform = value.Invert();
        }
    }

    /// <summary>
    /// This property holds the places the camera looks from and the instants it looks at.  A camera
    /// that asked for neither focal blur nor motion blur takes a single sample.
    /// </summary>
    public CameraSampler Sampler { get; }

    /// <summary>
    /// This property holds the inverse of the view transform, which carries a ray from the camera's
    /// own space out into the world.
    /// </summary>
    protected Matrix InverseTransform => _inverseTransform;

    /// <summary>
    /// This property holds the width of the image, in pixels.
    /// </summary>
    protected int Width { get; }

    /// <summary>
    /// This property holds the height of the image, in pixels.
    /// </summary>
    protected int Height { get; }

    /// <summary>
    /// This property holds the ratio of the image's width to its height.
    /// </summary>
    protected double Aspect { get; }

    private Matrix _transform;
    private Matrix _inverseTransform;

    protected PixelToRayConverter(RenderContext context, Matrix transform, CameraSampler sampler)
    {
        _transform = transform ?? Matrix.Identity;
        _inverseTransform = _transform.Invert();

        Sampler = sampler ?? new CameraSampler(0, 1);

        Width = context.Width;
        Height = context.Height;
        Aspect = Convert.ToDouble(Width) / Convert.ToDouble(Height);
    }

    /// <summary>
    /// This is a helper that places a pixel in a frame that runs from +1 at the left and top edges
    /// to -1 at the right and bottom, matching the perspective converter's sense that the camera's
    /// own +X points to the left of the image and +Y points up.  The projections that map a pixel
    /// to an angle rather than to a point on a plane build on this.
    /// </summary>
    /// <param name="x">The X coordinate of the pixel.</param>
    /// <param name="y">The Y coordinate of the pixel.</param>
    /// <param name="centerX">The X offset within the pixel to treat as center.</param>
    /// <param name="centerY">The Y offset within the pixel to treat as center.</param>
    /// <param name="shiftX">The amount to shift the X coordinate off center of the pixel.</param>
    /// <param name="shiftY">The amount to shift the Y coordinate off center of the pixel.</param>
    /// <returns>The pixel's place in the frame, each coordinate from -1 to 1.</returns>
    protected (double fx, double fy) FrameCoordinate(
        int x, int y, double centerX, double centerY, double shiftX, double shiftY)
    {
        return (
            1.0 - 2.0 * (Convert.ToDouble(x) + centerX + shiftX) / Convert.ToDouble(Width),
            1.0 - 2.0 * (Convert.ToDouble(y) + centerY + shiftY) / Convert.ToDouble(Height));
    }

    /// <summary>
    /// This method is used to generate a ray for the pixel at the given location.
    /// </summary>
    /// <param name="x">The X coordinate of the pixel to get the ray for.</param>
    /// <param name="y">The Y coordinate of the pixel to get the ray for.</param>
    /// <param name="centerX">The X offset within the pixel to treat as center.</param>
    /// <param name="centerY">The Y offset within the pixel to treat as center.</param>
    /// <param name="shiftX">The amount to shift the X coordinate off center of the pixel.</param>
    /// <param name="shiftY">The amount to shift the Y coordinate off center of the pixel.</param>
    /// <param name="sampleIndex">Which place across the lens to fire from, from zero up to the
    /// lens's sample count.  A pinhole has but the one.</param>
    /// <returns>The ray for the pixel.</returns>
    public abstract Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int sampleIndex = 0);
}
