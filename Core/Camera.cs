using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Core;

/// <summary>
/// This class represents a camera in the raytracer
/// </summary>
public class Camera
{
    /// <summary>
    /// This property holds the location of the camera.
    /// </summary>
    public Point Location { get; set; }

    /// <summary>
    /// This property holds the point the camera is looking at.
    /// </summary>
    public Point LookAt { get; set; }

    /// <summary>
    /// This property holds the "up" vector for the camera.
    /// </summary>
    public Vector Up { get; set; }

    /// <summary>
    /// This property holds the vertical field of view angle for the camera.
    /// </summary>
    public double VerticalFieldOfView { get; set; }

    /// <summary>
    /// This property holds the defocus angle value for the camera.
    /// </summary>
    public double DefocusAngle { get; set; }

    /// <summary>
    /// This property holds the focus distance value for the camera.
    /// </summary>
    public double FocusDistance { get; set; }

    private readonly double _imageWidth;
    private readonly double _imageHeight;

    private Vector _defocusDiskU;
    private Vector _defocusDiskV;
    private Vector _pixelDeltaX;
    private Vector _pixelDeltaY;
    private Point _originPixelLocation;

    public Camera(Image image)
    {
        _imageWidth = Convert.ToDouble(image.Width);
        _imageHeight = Convert.ToDouble(image.Height);

        Location = new Point(0, 0, -1);
        LookAt = new Point(0, 0, 0);
        Up = new Vector(0, 1, 0);
        VerticalFieldOfView = 90;
        DefocusAngle = 0;
        FocusDistance = 10;
        _defocusDiskU = null!;
        _defocusDiskV = null!;
        _pixelDeltaX = null!;
        _pixelDeltaY = null!;
        _originPixelLocation = Location;
    }

    /// <summary>
    /// This method is used to initialize the runtime properties of the camera.
    /// </summary>
    public void Initialize()
    {
        double h = Math.Tan(VerticalFieldOfView.ToRadians() / 2);
        double viewportHeight = h * 2 * FocusDistance;
        double viewportWidth = viewportHeight * (_imageWidth / _imageHeight);
        Vector w = (Location - LookAt).Unit();
        Vector u = Up.Cross(w).Unit();
        Vector v = w.Cross(u);
        Vector horizontalViewportVector = u * viewportWidth;
        Vector verticalViewportVector = -v * viewportHeight;

        _pixelDeltaX = horizontalViewportVector / _imageWidth;
        _pixelDeltaY = verticalViewportVector / _imageHeight;

        Point upperLeft = Location - w * FocusDistance -
                          horizontalViewportVector / 2 -
                          verticalViewportVector / 2;

        _originPixelLocation = upperLeft + 0.5 * (_pixelDeltaX + _pixelDeltaY);

        double halfAngle = DefocusAngle / 2;
        double defocusRadius = FocusDistance * Math.Tan(halfAngle.ToRadians());

        _defocusDiskU = u * defocusRadius;
        _defocusDiskV = v * defocusRadius;
    }

    /// <summary>
    /// This method is used to generate a ray for the given pixel location.
    /// </summary>
    /// <param name="pixel">The pixel to create a ray for.</param>
    /// <param name="jitter">If <c>false</c>, we return the ray for the center of
    /// the given pixel; if <c>true</c>, we return the ray for a point slightly
    /// offset from the center.</param>
    /// <returns>The proper ray for the pixel location.</returns>
    public Ray CreateRayFor(Pixel pixel, bool jitter)
    {
        Point pixelCenter = _originPixelLocation +
                            _pixelDeltaX * pixel.X +
                            _pixelDeltaY * pixel.Y;

        if (jitter)
        {
            double offsetX = DoubleExtensions.RandomDouble() - 0.5;
            double offsetY = DoubleExtensions.RandomDouble() - 0.5;
            Vector offset = _pixelDeltaX * offsetX + _pixelDeltaY * offsetY;

            pixelCenter += offset;
        }

        Point origin = DefocusAngle <= 0 ? Location : DefocusDiskSample();
        Vector direction = pixelCenter - Location;

        return new Ray(origin, direction);
    }

    /// <summary>
    /// This method generates a random origin point for a ray from within the defocus
    /// disk.
    /// </summary>
    /// <returns>The random ray origin.</returns>
    private Point DefocusDiskSample()
    {
        Vector vector = Vector.RandomInUnitDisk();
        Vector place = Location + _defocusDiskU * vector.X + _defocusDiskV * vector.Y;

        return new Point(place);
    }
}
