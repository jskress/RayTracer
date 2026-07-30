using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class is the fisheye projection: a circular, very wide one, where how far a pixel sits
/// from the middle sets the angle its ray leaves at.  The field of view is the angle across the
/// whole circle, and may be a half-circle or more, so the world curves away to the edges.  The
/// circle is fitted to the shorter side of the image, and the corners outside it are left to the
/// background.
/// <para>
/// A fisheye has no flat lens to gather across, so a scene that asks it for an aperture is warned
/// and gets a pinhole.  The shutter still works, so it may still be set moving.
/// </para>
/// </summary>
public class FisheyeRayConverter : PixelToRayConverter
{
    private readonly double _halfFieldOfView;

    public FisheyeRayConverter(
        RenderContext context, double fieldOfView, Matrix transform, CameraSampler sampler)
        : base(context, transform, sampler)
    {
        _halfFieldOfView = fieldOfView / 2;
    }

    /// <summary>
    /// This method is used to generate a ray for the pixel at the given location.
    /// </summary>
    public override Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int sampleIndex = 0)
    {
        (double fx, double fy) = FrameCoordinate(x, y, centerX, centerY, shiftX, shiftY);

        // Fit the circle to the shorter side of the image so it stays round rather than oval.
        double u = Aspect >= 1 ? fx * Aspect : fx;
        double v = Aspect >= 1 ? fy : fy / Aspect;
        double r = Math.Sqrt(u * u + v * v);

        Point origin = InverseTransform * Point.Zero;

        // Outside the circle there is nothing to show, so send the ray away from the scene and let
        // it come back the background color.
        if (r > 1)
        {
            Vector away = (InverseTransform * new Point(0, 0, 1) - origin).Unit;

            return new Ray(origin, away, sampleIndex);
        }

        // How far a pixel lies from the middle is how far its ray tips away from straight ahead;
        // which way it lies is which way the ray tips.
        double theta = r * _halfFieldOfView;
        double sinTheta = Math.Sin(theta);
        double dirX = r > 0 ? sinTheta * u / r : 0;
        double dirY = r > 0 ? sinTheta * v / r : 0;
        double dirZ = -Math.Cos(theta);

        Vector direction = (InverseTransform * new Point(dirX, dirY, dirZ) - origin).Unit;

        return new Ray(origin, direction, sampleIndex);
    }
}
