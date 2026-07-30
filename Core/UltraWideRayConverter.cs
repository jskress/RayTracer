using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class is the ultra-wide projection: a rectangular wide-angle one that fills the whole
/// frame rather than a circle, with less of the corner stretch a very wide perspective camera
/// has.  The field of view is the angle across the width; the height takes as much of it as the
/// image's shape calls for.  Each pixel maps to a longitude and a latitude, so the world curves
/// away toward the edges, but the frame stays a rectangle.
/// <para>
/// It has no flat lens to gather across, so a scene that asks it for an aperture is warned and
/// gets a pinhole.  The shutter still works, so it may still be set moving.
/// </para>
/// </summary>
public class UltraWideRayConverter : PixelToRayConverter
{
    private readonly double _halfHorizontal;
    private readonly double _halfVertical;

    public UltraWideRayConverter(
        RenderContext context, double fieldOfView, Matrix transform, CameraSampler sampler)
        : base(context, transform, sampler)
    {
        _halfHorizontal = fieldOfView / 2;
        _halfVertical = _halfHorizontal / Aspect;
    }

    /// <summary>
    /// This method is used to generate a ray for the pixel at the given location.
    /// </summary>
    public override Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int sampleIndex = 0)
    {
        (double fx, double fy) = FrameCoordinate(x, y, centerX, centerY, shiftX, shiftY);

        double longitude = fx * _halfHorizontal;
        double latitude = fy * _halfVertical;

        double cosLatitude = Math.Cos(latitude);
        double dirX = Math.Sin(longitude) * cosLatitude;
        double dirY = Math.Sin(latitude);
        double dirZ = -Math.Cos(longitude) * cosLatitude;

        Point origin = InverseTransform * Point.Zero;
        Vector direction = (InverseTransform * new Point(dirX, dirY, dirZ) - origin).Unit;

        return new Ray(origin, direction, sampleIndex);
    }
}
