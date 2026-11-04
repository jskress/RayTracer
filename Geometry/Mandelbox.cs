using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents Tom Lowe's Mandelbox (2010): the points of space that stay bounded when each round folds
/// the point back into a box, folds it back through a sphere, magnifies it and adds the point itself.
/// <para>
/// **The box fold** reflects each coordinate that has passed the fold limit back inside: past one, a coordinate
/// of 1.3 becomes 0.7.  **The sphere fold** turns a point inside the outer radius inside out about it, and one
/// inside the inner radius is magnified by as much as one at the inner radius would be -- which is what puts a
/// finer copy of the whole inside every hollow.  Then the point is multiplied by the factor and added to itself
/// as it started.  A factor of two gives a box of boxes with windows in every face; a negative one, -1.5 the
/// best known, a lacework of towers and arches.
/// </para>
/// <para>
/// The estimate of how far the surface is follows from what each round does to distances: the folds keep them or
/// shrink them by the sphere fold's own factor, and the magnifying multiplies them by the factor's size, so the
/// derivative is carried along as one number beside the point and the distance is <c>|z| / |z'|</c>.  It is never
/// less than nothing, so the surface is taken where it falls to the <see cref="Detail"/>, everything nearer the set
/// being inside, as sphere-traced Mandelboxes are drawn: taking every point that never runs away as inside drew
/// the exact edge of the set after so many rounds, finer than any pixel, a speckled dust.  A factor whose size is
/// one or less magnifies nothing and makes no fractal, and is refused.
/// </para>
/// <para>
/// **It knows its own extent**: for a factor <c>s</c> above one the box of half-width
/// <c>2 L (s + 1) / (s - 1)</c> (six, for a factor of two and a fold limit of one), and for one below minus one
/// <c>2 L</c>, as sampling the iteration shows, grown by four details since the surface stands off the set.  A
/// smaller box would cut the shape; a larger one would only coarsen the march, whose smallest step is a share of
/// the box.
/// </para>
/// </summary>
public class Mandelbox : DistanceEstimatedSurface
{
    // The least squared distance from the middle past which the iteration is taken to have run away: ten, or twice
    // the shape's reach if that is more.  It must be one a dozen rounds can reach -- at a factor of -1.5 they
    // magnify only 130 times.
    private const double LeastEscape = 100;

    /// <summary>
    /// This property holds how much each round magnifies the point: the Mandelbox's own "scale".
    /// </summary>
    public double Factor { get; set; } = 2;

    /// <summary>
    /// This property holds the radius inside which the sphere fold magnifies by its greatest.
    /// </summary>
    public double InnerRadius { get; set; } = 0.5;

    /// <summary>
    /// This property holds the radius inside which the sphere fold turns a point inside out.
    /// </summary>
    public double OuterRadius { get; set; } = 1;

    /// <summary>
    /// This property holds how far a coordinate may go before the box fold reflects it back.
    /// </summary>
    public double Fold { get; set; } = 1;

    /// <summary>
    /// This property holds how many rounds are run before a point that has not run away is taken to belong.
    /// </summary>
    public int Iterations { get; set; } = 12;

    /// <summary>
    /// This property holds how near the set a point must come to be taken as on it, in the shape's own units: the
    /// finest detail the surface shows.  A hundredth by default, for a shape a few units across; less shows finer
    /// lace, and too little a dust finer than the pixels.
    /// </summary>
    public double Detail { get; set; } = 0.01;

    /// <summary>
    /// This property holds how far either way the field is looked at to find which way the surface faces: half the
    /// detail.
    /// </summary>
    protected override double NormalStep => Math.Max(Accuracy * 8, Detail / 2);

    private BoundingBox _domain;
    private double _escapedAt;

    /// <summary>
    /// This method works out the region the march is confined to.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _domain = BoundingBox ?? GetDefaultBoundingBox();
        _escapedAt = Math.Max(LeastEscape, 4 * Reach * Reach);
    }

    /// <summary>
    /// This property holds how far from the middle the Mandelbox reaches along each axis, for its factor and fold.
    /// </summary>
    private double Reach => Factor > 1
        ? 2 * Fold * (Factor + 1) / (Factor - 1)
        : 2 * Fold;

    /// <summary>
    /// This method returns the box the Mandelbox lies within for its factor and fold.
    /// </summary>
    /// <returns>The box the fractal sits in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        // The surface stands off the set, at the detail, and the set reaches the box: unpadded, the box cut the
        // surface flat on every face.  Four details, since the estimate is short of the distance and the surface may
        // stand somewhat further off than one.
        double reach = Reach + 4 * Detail;

        return new BoundingBox()
            .Add(new Point(-reach, -reach, -reach))
            .Add(new Point(reach, reach, reach));
    }

    /// <summary>
    /// This property holds the region the march is confined to.
    /// </summary>
    protected override BoundingBox MarchingDomain => _domain;

    /// <summary>
    /// This method estimates how far the nearest surface is from a point: the folds and the magnifying run from it,
    /// the derivative carried beside them, until the point runs away or the rounds run out.
    /// </summary>
    protected override double DistanceAt(double x, double y, double z)
    {
        double inner = InnerRadius * InnerRadius;
        double outer = OuterRadius * OuterRadius;
        double size = Math.Abs(Factor);
        (double zx, double zy, double zz) = (x, y, z);
        double derivative = 1;
        bool escaped = false;

        for (int round = 0; round < Iterations && !escaped; round++)
        {
            zx = Math.Clamp(zx, -Fold, Fold) * 2 - zx;
            zy = Math.Clamp(zy, -Fold, Fold) * 2 - zy;
            zz = Math.Clamp(zz, -Fold, Fold) * 2 - zz;

            double squared = zx * zx + zy * zy + zz * zz;
            double folded = squared < inner ? outer / inner
                : squared < outer ? outer / squared
                : 1;

            zx = zx * folded * Factor + x;
            zy = zy * folded * Factor + y;
            zz = zz * folded * Factor + z;
            derivative = derivative * folded * size + 1;
            escaped = zx * zx + zy * zy + zz * zz > _escapedAt;
        }

        // The surface is where the estimate falls to the detail: inside it, the march goes by the sign.
        return Math.Sqrt(zx * zx + zy * zy + zz * zz) / Math.Abs(derivative) - Detail;
    }
}
