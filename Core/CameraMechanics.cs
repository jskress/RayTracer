using RayTracer.Basics;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class encapsulates the information how a camera will convert from 3D to 2D.
/// </summary>
public class CameraMechanics
{
    /// <summary>
    /// This property reports the width of the target image.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// This property reports the height of the target image.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// This property reports the field of view (in radians) for the camera.
    /// </summary>
    public double FieldOfView { get; }

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

    public CameraMechanics(Canvas canvas, double fieldOfView, Matrix transform = null!)
    {
        _transform = transform ?? Matrix.Identity;
        _inverseTransform = _transform.Invert();

        Width = canvas.Width;
        Height = canvas.Height;
        FieldOfView = fieldOfView;

        double width = Convert.ToDouble(Width);
        double halfView = Math.Tan(fieldOfView / 2);
        double aspectRatio = width / Convert.ToDouble(Height);

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
    /// <returns>The ray for the pixel.</returns>
    public Ray GetRayForPixel(int x, int y)
    {
        double xOffset = (Convert.ToDouble(x) + 0.5) * PixelSize;
        double yOffset = (Convert.ToDouble(y) + 0.5) * PixelSize;
        double worldX = _halfWidth - xOffset;
        double worldY = _halfHeight - yOffset;
        Point pixel = _inverseTransform * new Point(worldX, worldY, -1);
        Point origin = _inverseTransform * Point.Zero;
        Vector direction = (pixel - origin).Unit;

        return new Ray(origin, direction);
    }
}
