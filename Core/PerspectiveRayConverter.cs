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
    private readonly double _halfDiagonal;
    private readonly double _distortion;
    private readonly double _vignetting;

    public PerspectiveRayConverter(
        RenderContext context, double fieldOfView, Matrix transform = null, CameraSampler sampler = null)
        : this(context, fieldOfView, transform, sampler, 0, 0)
    {
    }

    /// <summary>
    /// This constructor makes a perspective projection whose lens bows straight lines, and vignettes; see
    /// <see cref="Camera.Distortion"/> and <see cref="Camera.Vignetting"/>.
    /// </summary>
    public PerspectiveRayConverter(
        RenderContext context, double fieldOfView, Matrix transform, CameraSampler sampler,
        double distortion, double vignetting)
        : base(context, transform, sampler)
    {
        _distortion = distortion;
        _vignetting = vignetting;

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
        _halfDiagonal = Math.Sqrt(_halfWidth * _halfWidth + _halfHeight * _halfHeight);
    }

    /// <summary>
    /// This method returns where, on the plane one unit out that the pixels lie on, a place in a pixel sends its ray,
    /// and how wide the ray is.
    /// <para>
    /// **A lens may bow straight lines** (see <see cref="Camera.Distortion"/>): a place toward the corners sends its
    /// ray further out than a pinhole would, as a wide-angle lens squeezes more of the world into its edges, or less
    /// far, as a long one does.  The stretch grows as the square of how far toward a corner the place is, a share of
    /// the picture's half-diagonal, so the middle is as it was and the field of view is still the middle's.  The ray
    /// widens with the stretch, as fast as it grows outward there, so what is seen through it is filtered as wide as
    /// the pixel now reaches.
    /// </para>
    /// </summary>
    private (double X, double Y, double Spread) PlaceOf(
        int x, int y, double centerX, double centerY, double shiftX, double shiftY)
    {
        double xOffset = (Convert.ToDouble(x) + centerX + shiftX) * PixelSize;
        double yOffset = (Convert.ToDouble(y) + centerY + shiftY) * PixelSize;
        double worldX = _halfWidth - xOffset;
        double worldY = _halfHeight - yOffset;

        if (_distortion == 0)
            return (worldX, worldY, PixelSize);

        double reach = (worldX * worldX + worldY * worldY) / (_halfDiagonal * _halfDiagonal);
        double stretch = 1 + _distortion * reach;

        return (worldX * stretch, worldY * stretch, PixelSize * (1 + 3 * _distortion * reach));
    }

    /// <summary>
    /// This method reports whether the light from a place on the lens reaches a place in a pixel, or is stopped by the
    /// lens's barrel (see <see cref="Camera.Vignetting"/>).
    /// <para>
    /// Seen from a place toward a picture's corner, a lens's opening is not round: the barrel in front of it cuts it
    /// off on one side, and what is left is where the two overlap -- a cat's eye, which is the shape a highlight out
    /// of focus takes there.  The barrel is taken as an opening as wide as the lens's own, standing off its middle
    /// in proportion to how far toward a corner the place is: at a corner, by the vignetting times the opening's
    /// radius.
    /// </para>
    /// </summary>
    public override bool Passes(
        int x, int y, double centerX, double centerY, double shiftX, double shiftY, int sampleIndex)
    {
        if (_vignetting <= 0 || Sampler.Aperture <= 0 || Sampler.SampleCount == 1)
            return true;

        (double worldX, double worldY, _) = PlaceOf(x, y, centerX, centerY, shiftX, shiftY);
        (double lensX, double lensY) = Sampler.OffsetFor(sampleIndex);
        double share = _vignetting / _halfDiagonal;
        double offX = lensX - worldX * share;
        double offY = lensY - worldY * share;

        return offX * offX + offY * offY <= 1;
    }

    /// <summary>
    /// This method returns how much of the light a pixel's middle gets through a lens that vignettes: the natural
    /// falloff, the fourth power of the cosine of how far off the axis the pixel looks, blended in by the camera's
    /// vignetting.  Light reaching a place off the axis comes at a slant, from further off, and through an opening
    /// seen foreshortened, each costing a cosine or two of it.
    /// </summary>
    public override double FalloffAt(int x, int y)
    {
        if (_vignetting <= 0)
            return 1;

        (double worldX, double worldY, _) = PlaceOf(x, y, 0.5, 0.5, 0, 0);
        double cosSquared = 1 / (1 + worldX * worldX + worldY * worldY);

        return 1 - _vignetting + _vignetting * cosSquared * cosSquared;
    }

    /// <summary>
    /// This method is used to generate a ray for the pixel at the given location.
    /// </summary>
    public override Ray GetRayForPixel(
        int x, int y, double centerX = 0.5, double centerY = 0.5,
        double shiftX = 0, double shiftY = 0, int sampleIndex = 0)
    {
        (double worldX, double worldY, double spread) = PlaceOf(x, y, centerX, centerY, shiftX, shiftY);

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

            // The spread is the pixel's own size, which is what makes this a cone rather than a
            // line: the pixels lie one unit out, so a pixel `PixelSize` across subtends that many
            // radians.  It carries the width a pattern needs and nothing else -- the geometry of
            // this ray is untouched, and this path is still the one a scene with no lens stays on.
            return new Ray(
                pinholeOrigin, (pinholePixel - pinholeOrigin).Unit, sampleIndex, spread);
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

        return new Ray(origin, (target - origin).Unit, sampleIndex, spread);
    }
}
