using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class is the orthographic projection: a parallel one, where nothing shrinks with
/// distance, as a technical drawing does not.  Every ray leaves a different place on the image
/// plane but all point the same way, so two things of a size come out the same size however far
/// apart they lie.
/// <para>
/// It has no perspective field of view of its own, so it borrows the camera's: the view is made
/// as wide as a perspective camera of the same field of view would see at the distance it is
/// focused on, which is usually what it is aimed at.  That way a scene may swap between the two
/// and keep its subject the same size.
/// </para>
/// </summary>
public class OrthographicRayConverter : PixelToRayConverter
{
    private readonly double _halfWidth;
    private readonly double _halfHeight;

    public OrthographicRayConverter(
        RenderContext context, double fieldOfView, Matrix transform, CameraSampler sampler)
        : base(context, transform, sampler)
    {
        double halfView = Math.Tan(fieldOfView / 2) * Sampler.FocalDistance;

        if (Aspect >= 1)
        {
            _halfWidth = halfView;
            _halfHeight = halfView / Aspect;
        }
        else
        {
            _halfWidth = halfView * Aspect;
            _halfHeight = halfView;
        }
    }

    /// <summary>
    /// This method is used to generate a ray for the pixel at the given location.
    /// </summary>
    public override Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int sampleIndex = 0)
    {
        (double fx, double fy) = FrameCoordinate(x, y, centerX, centerY, shiftX, shiftY);
        double worldX = fx * _halfWidth;
        double worldY = fy * _halfHeight;

        // With no lens, every ray leaves its own spot on the image plane and points straight ahead,
        // so the rays run parallel and nothing converges.
        if (Sampler.Aperture <= 0)
        {
            Point origin = InverseTransform * new Point(worldX, worldY, 0);
            Point ahead = InverseTransform * new Point(worldX, worldY, -1);

            return new Ray(origin, (ahead - origin).Unit, sampleIndex);
        }

        // The lens gathers a parallel camera the same way it does a perspective one: each sample
        // starts from a different place across the lens but aims at the one point on the focal
        // plane the pixel's own ray passes through, so that plane stays sharp and the rest blurs.
        double focalDistance = Sampler.FocalDistance;
        (double lensX, double lensY) = Sampler.OffsetFor(sampleIndex);

        lensX *= Sampler.Aperture;
        lensY *= Sampler.Aperture;

        Point lensOrigin = InverseTransform * new Point(worldX + lensX, worldY + lensY, 0);
        Point target = InverseTransform * new Point(worldX, worldY, -focalDistance);

        return new Ray(lensOrigin, (target - lensOrigin).Unit, sampleIndex);
    }
}
