using RayTracer.Basics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a quaternion Julia set: the points that stay bounded when
/// <c>q -> q² + c</c> is applied over and over, with <c>c</c> a quaternion the scene chooses.
/// <para>
/// **There is no equation to solve here.**  A sphere, a torus, even a superellipsoid can be asked
/// where a ray meets it and will answer; this cannot, because whether a point belongs is settled by
/// iterating until the answer runs away, and no closed form says when that happens.  What can be had
/// instead is an *estimate* of how far the nearest surface is -- and that is enough to walk a ray by,
/// which is what <see cref="DistanceEstimatedSurface"/> does.
/// </para>
/// <para>
/// The estimate is the standard one for an escaping iteration: <c>0.5 · |q| · ln|q| / |q'|</c>, where
/// <c>q'</c> is the derivative carried along beside the iteration.  It says how far the surface is
/// *at least*, which is exactly the promise sphere tracing needs.
/// </para>
/// <para>
/// **It needs its numbers, unlike the quadrics.**  A paraboloid takes none because scaling covers its
/// whole family; here <c>c</c> *is* the shape -- move it and you get a different object, not the same
/// one resized -- so there is nothing a transform could stand in for.
/// </para>
/// <para>
/// It also bounds itself: every quaternion Julia set of this form lies inside a ball of radius two,
/// so unlike a <see cref="SignedDistanceSurface"/> it needs no <c>bounded by</c> from the scene.
/// </para>
/// </summary>
public class JuliaFractal : DistanceEstimatedSurface
{
    /// <summary>
    /// Beyond this, the iteration is taken to have run away and never to come back.  Four is the
    /// usual choice: the set itself lies within a radius of two, so a point that has reached four is
    /// past any hope of returning.
    /// </summary>
    private const double EscapedAt = 16;

    /// <summary>
    /// This property holds the quaternion the iteration adds each time round, as four numbers.  It is
    /// the shape itself: there is no scaling that turns one choice into another.
    /// </summary>
    public double[] C { get; set; } = [-0.2, 0.6, 0.2, 0.0];

    /// <summary>
    /// This property holds how many times the iteration is applied before a point that has not run
    /// away is taken to belong.  More gives a finer edge and costs time; ten or so is plenty to look
    /// at, and the difference above twenty is hard to see.
    /// </summary>
    public int Iterations { get; set; } = 10;

    private BoundingBox _domain;

    /// <summary>
    /// This method works out the region the march is confined to, which the shape knows for itself.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        _domain = BoundingBox ?? GetDefaultBoundingBox();
    }

    /// <summary>
    /// This method returns the ball of radius two that every set of this form lies inside.
    /// </summary>
    /// <returns>The box the fractal sits in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        return new BoundingBox()
            .Add(new Point(-2, -2, -2))
            .Add(new Point(2, 2, 2));
    }

    /// <summary>
    /// This property holds the region the march is confined to.
    /// </summary>
    protected override BoundingBox MarchingDomain => _domain;

    /// <summary>
    /// This method estimates how far the nearest surface is from a point.
    /// <para>
    /// The point becomes the first three parts of a quaternion, with the fourth left at nought -- a
    /// three-dimensional slice through what is really a four-dimensional object, which is the only
    /// way to look at one.  The iteration is then run, carrying the derivative alongside it, until
    /// the point escapes or the count runs out.
    /// </para>
    /// </summary>
    protected override double DistanceAt(double x, double y, double z)
    {
        (double qa, double qb, double qc, double qd) = (x, y, z, 0.0);

        // The derivative of the iteration with respect to the starting point, carried along as a
        // quaternion of its own and started at one.
        (double da, double db, double dc, double dd) = (1.0, 0.0, 0.0, 0.0);
        double magnitudeSquared = qa * qa + qb * qb + qc * qc + qd * qd;

        for (int step = 0; step < Iterations && magnitudeSquared < EscapedAt; step++)
        {
            // The derivative first, since it is worked out from the point *before* this step:
            // d' = 2·q·d.
            (da, db, dc, dd) = (
                2 * (qa * da - qb * db - qc * dc - qd * dd),
                2 * (qa * db + qb * da + qc * dd - qd * dc),
                2 * (qa * dc - qb * dd + qc * da + qd * db),
                2 * (qa * dd + qb * dc - qc * db + qd * da));

            // Then the point itself: q' = q² + c.  A quaternion squared is cheaper than the general
            // product, the cross terms cancelling.
            (qa, qb, qc, qd) = (
                qa * qa - qb * qb - qc * qc - qd * qd + C[0],
                2 * qa * qb + C[1],
                2 * qa * qc + C[2],
                2 * qa * qd + C[3]);

            magnitudeSquared = qa * qa + qb * qb + qc * qc + qd * qd;
        }

        double derivative = Math.Sqrt(da * da + db * db + dc * dc + dd * dd);

        // A point whose derivative has collapsed is one the iteration has flattened altogether, deep
        // inside the set; there is no scale left to measure with, so it is simply reported as inside.
        if (derivative < 1e-12)
            return -Accuracy;

        double magnitude = Math.Sqrt(magnitudeSquared);

        // A point that never grew at all sits at the very middle of the set, where the logarithm
        // below would go the wrong way.
        if (magnitude < 1e-12)
            return -Accuracy;

        return 0.5 * magnitude * Math.Log(magnitude) / derivative;
    }
}
