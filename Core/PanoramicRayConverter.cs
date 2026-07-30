using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class is the panoramic projection: a cylindrical one, for a wide horizontal sweep.  The
/// width wraps around as a longitude -- the field of view is how far around it reaches, and may be
/// a good deal more than a perspective camera could show -- while the height is a straight, upright
/// projection, so a wide sweep is had without the uprights of the world leaning the way a very wide
/// perspective makes them.
/// <para>
/// A cylinder has no lens, so a scene that gives one an aperture is warned and gets a pinhole; the
/// shutter still works.
/// </para>
/// </summary>
public class PanoramicRayConverter : PixelToRayConverter
{
    private readonly double _halfHorizontal;

    public PanoramicRayConverter(
        RenderContext context, double fieldOfView, Matrix transform, CameraSampler sampler)
        : base(context, transform, sampler)
    {
        _halfHorizontal = fieldOfView / 2;
    }

    /// <summary>
    /// This method is used to generate a ray for the pixel at the given location.
    /// </summary>
    public override Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int sampleIndex = 0)
    {
        (double fx, double fy) = FrameCoordinate(x, y, centerX, centerY, shiftX, shiftY);

        // Around the width is a longitude; up the height is a straight rise, taken flat rather than
        // as an angle, which is what keeps the world's uprights upright.
        double longitude = fx * _halfHorizontal;
        double rise = fy * _halfHorizontal / Aspect;

        double dirX = Math.Sin(longitude);
        double dirY = rise;
        double dirZ = -Math.Cos(longitude);

        Point origin = InverseTransform * Point.Zero;
        Vector direction = (InverseTransform * new Point(dirX, dirY, dirZ) - origin).Unit;

        return new Ray(origin, direction, sampleIndex);
    }
}
