using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Core;

/// <summary>
/// This class is the ordinary perspective projection: the one a pinhole gives, where every ray
/// leaves the one point and the world shrinks with distance.  It is what a camera is unless a
/// scene asks for another sort.
/// </summary>
public class PerspectiveRayConverter : PixelToRayConverter
{
    /// <summary>
    /// This property notes the size, in world space, of a pixel.
    /// </summary>
    public double PixelSize { get; }

    private readonly double _halfWidth;
    private readonly double _halfHeight;

    public PerspectiveRayConverter(
        RenderContext context, double fieldOfView, Matrix transform = null,
        CameraSampler sampler = null)
        : base(context, transform, sampler)
    {
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
    public override Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int sampleIndex = 0)
    {
        double xOffset = (Convert.ToDouble(x) + centerX + shiftX) * PixelSize;
        double yOffset = (Convert.ToDouble(y) + centerY + shiftY) * PixelSize;
        double worldX = _halfWidth - xOffset;
        double worldY = _halfHeight - yOffset;

        // A lens with no width to it fires from the one point, exactly as it always did, down to
        // the arithmetic.  Running it through the reckoning below with an offset of nothing would
        // give the same ray in geometry but not in the last bits of it, since the point it aims at
        // would be scaled by the focal distance and then normalized back, and every existing
        // picture would shift by a level here and there.  This is the path a scene wanting motion
        // blur and nothing else stays on, so the parts of it that hold still are traced by the same
        // arithmetic they would be without any blur at all.
        if (Sampler.Aperture <= 0)
        {
            Point pinholePixel = InverseTransform * new Point(worldX, worldY, -1);
            Point pinholeOrigin = InverseTransform * Point.Zero;

            return new Ray(
                pinholeOrigin, (pinholePixel - pinholeOrigin).Unit, sampleIndex);
        }

        // The ray through the middle of the lens crosses the focal plane at the focal distance
        // along its way; since the plane the pixels lie on is one unit out, scaling by that
        // distance lands on it.  Every sample is aimed at that same spot from a different place
        // across the lens, so what sits there is hit by all of them and stays sharp, and what does
        // not is smeared across as many places as there are samples.
        double focalDistance = Sampler.FocalDistance;
        (double lensX, double lensY) = Sampler.OffsetFor(sampleIndex);

        lensX *= Sampler.Aperture;
        lensY *= Sampler.Aperture;

        Point target = InverseTransform * new Point(
            worldX * focalDistance, worldY * focalDistance, -focalDistance);
        Point origin = InverseTransform * new Point(lensX, lensY, 0);

        return new Ray(origin, (target - origin).Unit, sampleIndex);
    }
}
