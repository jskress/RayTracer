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

    public PixelToRayConverter(RenderContext context, double fieldOfView, Matrix transform = null)
    {
        _transform = transform ?? Matrix.Identity;
        _inverseTransform = _transform.Invert();

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
    /// <returns>The ray for the pixel.</returns>
    public Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0)
    {
        double xOffset = (Convert.ToDouble(x) + centerX + shiftX) * PixelSize;
        double yOffset = (Convert.ToDouble(y) + centerY + shiftY) * PixelSize;
        double worldX = _halfWidth - xOffset;
        double worldY = _halfHeight - yOffset;
        Point pixel = _inverseTransform * new Point(worldX, worldY, -1);
        Point origin = _inverseTransform * Point.Zero;
        Vector direction = (pixel - origin).Unit;

        return new Ray(origin, direction);
    }
}
