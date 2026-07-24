using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class encapsulates the information how a camera will convert from 3D to 2D.
/// </summary>
public class PixelToRayConverter
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
    /// This property notes the size, in world space, of a pixel.
    /// </summary>
    public double PixelSize { get; }

    private readonly double _halfWidth;
    private readonly double _halfHeight;

    private Matrix _transform;
    private Matrix _inverseTransform;

    /// <summary>
    /// This property holds the lens rays are taken through.  A camera that asked for no focal blur
    /// has a pinhole for a lens, whose sample count is one.
    /// </summary>
    public Lens Lens { get; }

    public PixelToRayConverter(
        RenderContext context, double fieldOfView, Matrix transform = null, Lens lens = null)
    {
        _transform = transform ?? Matrix.Identity;
        _inverseTransform = _transform.Invert();

        Lens = lens ?? new Lens(0, 1);

        double width = Convert.ToDouble(context.Width);
        double halfView = Math.Tan(fieldOfView / 2);
        double aspectRatio = width / Convert.ToDouble(context.Height);

        if (aspectRatio >= 1)
        {
            _halfWidth = halfView;
            _halfHeight = halfView / aspectRatio;
        }
        else
        {
            _halfWidth = halfView * aspectRatio;
            _halfHeight = halfView;
        }

        PixelSize = _halfWidth * 2 / width;
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
    /// <param name="lensIndex">Which place across the lens to fire from, from zero up to the
    /// lens's sample count.  A pinhole has but the one.</param>
    /// <returns>The ray for the pixel.</returns>
    public Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int lensIndex = 0)
    {
        double xOffset = (Convert.ToDouble(x) + centerX + shiftX) * PixelSize;
        double yOffset = (Convert.ToDouble(y) + centerY + shiftY) * PixelSize;
        double worldX = _halfWidth - xOffset;
        double worldY = _halfHeight - yOffset;

        // A pinhole is left exactly as it always was, down to the arithmetic.  Running it through
        // the lens reckoning below with an offset of nothing would give the same ray in geometry
        // but not in the last bits of it, since the point it aims at would be scaled by the focal
        // distance and then normalized back, and every existing picture would shift by a level
        // here and there.
        if (Lens.SampleCount == 1)
        {
            Point pinholePixel = _inverseTransform * new Point(worldX, worldY, -1);
            Point pinholeOrigin = _inverseTransform * Point.Zero;

            return new Ray(pinholeOrigin, (pinholePixel - pinholeOrigin).Unit);
        }

        // The ray through the middle of the lens crosses the focal plane at the focal distance
        // along its way; since the plane the pixels lie on is one unit out, scaling by that
        // distance lands on it.  Every sample is aimed at that same spot from a different place
        // across the lens, so what sits there is hit by all of them and stays sharp, and what does
        // not is smeared across as many places as there are samples.
        double focalDistance = Lens.FocalDistance;
        (double lensX, double lensY) = Lens.OffsetFor(lensIndex);

        lensX *= Lens.Aperture;
        lensY *= Lens.Aperture;

        Point target = _inverseTransform * new Point(
            worldX * focalDistance, worldY * focalDistance, -focalDistance);
        Point origin = _inverseTransform * new Point(lensX, lensY, 0);

        return new Ray(origin, (target - origin).Unit);
    }
}
