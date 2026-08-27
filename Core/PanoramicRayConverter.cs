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
    private readonly double _perPixel;

    public PanoramicRayConverter(
        RenderContext context, double fieldOfView, Matrix transform, CameraSampler sampler)
        : base(context, transform, sampler)
    {
        _halfHorizontal = fieldOfView / 2;

        // How much of the sky one pixel covers, before the rise is taken into account.  Around the
        // width a pixel is this much longitude; up the height it is the same number again, since the
        // rise is scaled by the aspect that relates width to height.
        _perPixel = _halfHorizontal * 2.0 / Width;
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

        // Up the height the picture is a straight rise rather than an angle, so a pixel high up
        // covers *less* sky than one at eye level -- the ray it names is longer before it is made a
        // unit vector, and the same step across the picture turns it less far.  Sideways that
        // shortening tells once, and vertically it tells twice, so the sideways figure is the wider
        // and is the one to filter by.
        return new Ray(
            origin, direction, sampleIndex, _perPixel / Math.Sqrt(1 + rise * rise));
    }
}
