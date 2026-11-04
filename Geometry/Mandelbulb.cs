using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents the Mandelbulb: the points of space that stay bounded when <c>z -> z^n + p</c> is applied
/// over and over, <c>p</c> being the point itself and the power taken in White and Nylander's "triplex" way --
/// length to the nth power, both angles of the point's direction multiplied by n.  It is what the Mandelbrot set
/// becomes when the plane is made space.
/// <para>
/// Like a <see cref="JuliaFractal"/>, it cannot be asked where a ray meets it, only how far off it is at least,
/// which is enough to walk a ray by (see <see cref="DistanceEstimatedSurface"/>).  The estimate is the usual one for
/// an escaping iteration, <c>0.5 · |z| · ln|z| / |z'|</c>, the derivative carried along as one number: each round
/// multiplies it by <c>n |z|^(n-1)</c> and adds one, the point being added again every round.
/// </para>
/// <para>
/// **The surface stands a little off the set**, where the estimate falls to the <see cref="Detail"/>, and its
/// facing is looked at over half that: the set's own edge has crinkles at every scale, and drawn exactly, every
/// pixel's facing and shadow fell at random among crinkles far finer than itself, a speckled dust.  This is how
/// sphere-traced fractals are drawn, and the detail is the scene's to make finer for a closer look.
/// </para>
/// <para>
/// **Its axis is Y**, up in a scene, where the formula's own is Z: the bulb's pole points up, and the ring of
/// lobes round its middle lies flat.  It lies within a ball of radius two, whatever the power, and so needs no
/// <c>bounded by</c>.
/// </para>
/// </summary>
public class Mandelbulb : DistanceEstimatedSurface
{
    // Past this distance from the middle the iteration has run away and will not come back.
    private const double EscapedAt = 2;

    /// <summary>
    /// This property holds the power the point is raised to each round.  Eight is the bulb everyone knows, its eight
    /// lobes round the middle; two is a blob with a seam, and higher powers more lobes, finer.
    /// </summary>
    public double Power { get; set; } = 8;

    /// <summary>
    /// This property holds how many rounds are run before a point that has not run away is taken to belong.  More
    /// gives finer detail at the cost of time; twelve shows the bulb well.
    /// </summary>
    public int Iterations { get; set; } = 12;

    /// <summary>
    /// This property holds how near the set a point must come to be taken as on it, in the shape's own units: the
    /// finest detail the surface shows, and what its facing is looked at over.  Detail far finer than a pixel is
    /// drawn as speckle, every pixel's facing and shadow at random among crinkles it cannot show.
    /// </summary>
    public double Detail { get; set; } = 0.002;

    /// <summary>
    /// This property holds how far either way the field is looked at to find which way the surface faces: half the
    /// detail.
    /// </summary>
    protected override double NormalStep => Math.Max(Accuracy * 8, Detail / 2);

    private BoundingBox _domain;

    /// <summary>
    /// This method works out the region the march is confined to.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _domain = BoundingBox ?? GetDefaultBoundingBox();
    }

    /// <summary>
    /// This method returns the box round the ball of radius two the bulb lies within.
    /// </summary>
    /// <returns>The box the fractal sits in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        // Grown by four details, as the surface stands off the set; see Mandelbox.
        double reach = EscapedAt + 4 * Detail;

        return new BoundingBox()
            .Add(new Point(-reach, -reach, -reach))
            .Add(new Point(reach, reach, reach));
    }

    /// <summary>
    /// This property holds the region the march is confined to.
    /// </summary>
    protected override BoundingBox MarchingDomain => _domain;

    /// <summary>
    /// This method estimates how far the nearest surface is from a point: the iteration run from it, the derivative
    /// carried beside it, until the point runs away or the rounds run out.
    /// </summary>
    protected override double DistanceAt(double x, double y, double z)
    {
        // The formula's axis is Z, and the scene's up is Y.
        (double px, double py, double pz) = (x, z, y);
        (double zx, double zy, double zz) = (px, py, pz);
        double derivative = 1;
        double radius = Math.Sqrt(zx * zx + zy * zy + zz * zz);

        for (int round = 0; round < Iterations && radius <= EscapedAt; round++)
        {
            derivative = Math.Pow(radius, Power - 1) * Power * derivative + 1;

            if (radius < 1e-300)
            {
                // Nought to any power is nought: the point is simply added again.
                (zx, zy, zz) = (px, py, pz);
            }
            else
            {
                double theta = Math.Acos(Math.Clamp(zz / radius, -1, 1)) * Power;
                double phi = Math.Atan2(zy, zx) * Power;
                double length = Math.Pow(radius, Power);
                double sinTheta = Math.Sin(theta);

                zx = length * sinTheta * Math.Cos(phi) + px;
                zy = length * sinTheta * Math.Sin(phi) + py;
                zz = length * Math.Cos(theta) + pz;
            }

            radius = Math.Sqrt(zx * zx + zy * zy + zz * zz);
        }

        // A point that never ran away belongs; one that did is on the surface where the estimate falls to the detail.
        return radius > EscapedAt ? 0.5 * radius * Math.Log(radius) / derivative - Detail : -Detail;
    }
}
