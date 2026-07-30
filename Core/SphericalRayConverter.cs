using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class is the spherical projection: a full look all around, the whole sphere unrolled onto
/// the rectangle -- the width a complete turn of 360 degrees, the height a half turn of 180, from
/// straight down at the bottom to straight up at the top.  It is what an environment map wants: a
/// single image that holds every direction at once.
/// <para>
/// The whole sphere is always shown, so a spherical camera does not take a field of view.  It has
/// no lens either, so a scene that gives it an aperture is warned and gets a pinhole; the shutter
/// still works.
/// </para>
/// </summary>
public class SphericalRayConverter : PixelToRayConverter
{
    public SphericalRayConverter(RenderContext context, Matrix transform, CameraSampler sampler)
        : base(context, transform, sampler) {}

    /// <summary>
    /// This method is used to generate a ray for the pixel at the given location.
    /// </summary>
    public override Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int sampleIndex = 0)
    {
        (double fx, double fy) = FrameCoordinate(x, y, centerX, centerY, shiftX, shiftY);

        // Across the width is a whole turn, up the height a half turn: the two together name every
        // direction there is.
        double longitude = fx * Math.PI;
        double latitude = fy * (Math.PI / 2);

        double cosLatitude = Math.Cos(latitude);
        double dirX = Math.Sin(longitude) * cosLatitude;
        double dirY = Math.Sin(latitude);
        double dirZ = -Math.Cos(longitude) * cosLatitude;

        Point origin = InverseTransform * Point.Zero;
        Vector direction = (InverseTransform * new Point(dirX, dirY, dirZ) - origin).Unit;

        return new Ray(origin, direction, sampleIndex);
    }
}
