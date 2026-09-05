using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class carries a ray into the plane of a tapered extrusion's outline, and carries a hit
/// in that plane back out to a distance along the original ray.
/// <para>
/// A taper that changes the outline's scale linearly with height makes the solid a cone over that
/// outline: every cross-section is the same 2D shape, only scaled.  Seen from the apex, a ray
/// still projects to a straight <i>line</i> in the outline's plane, so the 2D segment
/// intersection an untapered extrusion already does applies here without change.  What does
/// change is the parameter along it.  Where the straight case projects along Y -- a parallel
/// projection, so a 2D point carries back to a distance with one division -- the tapered case
/// projects from the apex, and a central projection makes that a ratio instead.
/// </para>
/// <para>
/// The line itself comes from asking which one every projected point satisfies.  A point at
/// distance t is (Ox + t*Dx, Oz + t*Dz) divided by the scale (a + t*b) there, so demanding that
/// alpha*u + beta*v + gamma be nought for every t gives two equations -- one from the constant
/// term, one from the term in t -- and the coefficients that satisfy both are the cross product
/// of (Ox, Oz, a) with (Dx, Dz, b).
/// </para>
/// </summary>
internal class TaperedProjection
{
    /// <summary>
    /// This property holds the ray, projected into the plane of the outline.  It is <c>null</c>
    /// when the ray runs along the taper's own axis, since such a ray projects to a single point
    /// rather than a line and so meets no wall; it leaves through a cap instead.
    /// </summary>
    internal TwoDRay Line { get; }

    private readonly Ray _ray;
    private readonly double _scaleAtOrigin;
    private readonly double _scaleRate;

    internal TaperedProjection(Ray ray, double minimumY, double taperRate)
    {
        double originX = ray.Origin.X;
        double originZ = ray.Origin.Z;
        double directionX = ray.Direction.X;
        double directionZ = ray.Direction.Z;

        _ray = ray;
        _scaleAtOrigin = 1 + taperRate * (ray.Origin.Y - minimumY);
        _scaleRate = taperRate * ray.Direction.Y;

        double alpha = originZ * _scaleRate - _scaleAtOrigin * directionZ;
        double beta = _scaleAtOrigin * directionX - originX * _scaleRate;
        double gamma = originX * directionZ - originZ * directionX;

        // The three coefficients together are a cross product, so their combined size is bounded
        // by the product of the two vectors' sizes.  Measuring the two that name the line's
        // direction against that bound is what keeps this test independent of scale: a ray
        // carried into a surface that has been scaled up arrives short, and an absolute floor
        // would call it degenerate when it is merely small.
        double originSize = originX * originX + originZ * originZ + _scaleAtOrigin * _scaleAtOrigin;
        double directionSize = directionX * directionX + directionZ * directionZ +
                               _scaleRate * _scaleRate;

        Line = (alpha * alpha + beta * beta).IsNegligibleSquaredBeside(originSize * directionSize)
            ? null
            : new TwoDRay
            {
                // A line written as alpha*u + beta*v + gamma = 0 runs along (-beta, alpha), and
                // any point on it will serve as an origin.  Solving for whichever of the two
                // coordinates carries the larger coefficient keeps the most precision.
                Origin = Math.Abs(alpha) >= Math.Abs(beta)
                    ? new TwoDPoint(-gamma / alpha, 0)
                    : new TwoDPoint(0, -gamma / beta),
                // The direction is made unit length rather than left as it falls out.  A curve
                // segment decides which way to solve by comparing the ray's two ends with an
                // absolute test, so a line handed to it with an arbitrarily small direction --
                // and these coefficients carry the sizes of the ray's origin and direction
                // multiplied together -- could be read as running straight along an axis when it
                // does not.
                Direction = new TwoDVector(-beta, alpha).Unit
            };
    }

    /// <summary>
    /// This method carries a point in the plane of the outline back out to the distance along the
    /// original ray where it was seen.  If no such distance can be told apart from the noise --
    /// which happens only for a ray that cannot reach the point at all -- then <c>NaN</c> is
    /// returned.
    /// </summary>
    /// <param name="point">The point, on the unscaled outline, that the ray was seen to cross.</param>
    /// <returns>The distance along the ray, or <c>NaN</c>.</returns>
    internal double DistanceTo(TwoDPoint point)
    {
        // Rearranging u = (Ox + t*Dx) / (a + t*b) for t gives t = (Ox - u*a) / (u*b - Dx), and
        // the same in Z.  Either will do, so the one with the larger divisor is taken -- the same
        // reasoning, and the same trap avoided, as an untapered extrusion following whichever
        // axis its ray travels faster in.
        double divisorX = point.X * _scaleRate - _ray.Direction.X;
        double divisorZ = point.Y * _scaleRate - _ray.Direction.Z;
        bool useX = Math.Abs(divisorX) >= Math.Abs(divisorZ);
        double divisor = useX ? divisorX : divisorZ;

        if ((divisor * divisor).IsNegligibleSquaredBeside(
                divisorX * divisorX + divisorZ * divisorZ +
                _ray.Direction.X * _ray.Direction.X + _ray.Direction.Z * _ray.Direction.Z))
            return double.NaN;

        return useX
            ? (_ray.Origin.X - point.X * _scaleAtOrigin) / divisor
            : (_ray.Origin.Z - point.Y * _scaleAtOrigin) / divisor;
    }
}
