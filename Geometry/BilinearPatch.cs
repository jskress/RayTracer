using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a bilinear patch: the warped quadrilateral spanned by four corners.
/// <para>
/// A point on it is found by mixing the corners along one direction and then the other, so the
/// surface is <c>(1-u)(1-v)*P00 + u(1-v)*P10 + u*v*P11 + (1-u)v*P01</c>.  Four corners in a plane
/// give a flat quadrilateral, exactly what a <see cref="Parallelogram"/> already draws; lift one
/// of them out of that plane and the patch becomes the doubly-ruled saddle between them.  Both
/// families of straight lines lie in the surface, which is why a sail, a flag, a warped panel or a
/// twisted ribbon are all naturally one of these.
/// </para>
/// <para>
/// **It is solved exactly, not walked.**  Writing the surface equal to a point on the ray and
/// eliminating the distance leaves a *quadratic in u*, so the crossings come from one square root
/// rather than from subdividing as a <see cref="BicubicPatch"/> or a <see cref="Parametric"/> must.
/// The arrangement used here is Reshetov's, from "Cool Patches: A Geometric Approach to Ray/Bilinear
/// Patch Intersections" (Ray Tracing Gems, 2019), which gets the three coefficients out of cross
/// products of the edges rather than by expanding the polynomial, and picks the root pair the way
/// a stable quadratic solver does so that the small root keeps its precision.
/// </para>
/// </summary>
public class BilinearPatch : Surface
{
    /// <summary>
    /// This surface is a sheet: it encloses nothing, so its normal points whichever way its
    /// corners were written rather than naming an outside.  See <see cref="Surface.IsASheet"/>.
    /// </summary>
    public override bool IsASheet => true;

    /// <summary>
    /// This property provides the four corners of the patch.  They are taken **in order around
    /// the quadrilateral** -- P00, P10, P11, P01 -- not as opposite pairs; given in the wrong
    /// order they describe a bow tie, which is a real surface but rarely the wanted one.
    /// </summary>
    public Point[] Corners { get; set; }

    private Point _p00;
    private Point _p10;
    private Point _p11;
    private Point _p01;

    /// <summary>
    /// This method is called once prior to rendering to give the surface a chance to perform any
    /// expensive precomputing that will help ray/intersection tests go faster.
    /// </summary>
    protected override void PrepareSurfaceForRendering()
    {
        if (Corners is not { Length: 4 })
            throw new Exception("A bilinear patch requires exactly four corner points.");

        _p00 = Corners[0];
        _p10 = Corners[1];
        _p11 = Corners[2];
        _p01 = Corners[3];
    }

    /// <summary>
    /// This method is used to produce a default bounding box for this shape.  A bilinear patch
    /// never leaves the convex hull of its own corners -- every point on it is a weighted mix of
    /// them, with the weights never negative -- so the box around those four holds all of it.
    /// </summary>
    /// <returns>A default bounding box for the patch.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        if (Corners is not { Length: 4 })
            return null;

        BoundingBox box = new BoundingBox();

        foreach (Point corner in Corners)
            box.Add(corner);

        return box;
    }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the patch and, if so,
    /// where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        Vector e10 = _p10 - _p00;
        Vector e11 = _p11 - _p10;
        Vector e00 = _p01 - _p00;
        Vector normal = e10.Cross(_p01 - _p11);
        Vector q00 = _p00 - ray.Origin;
        Vector q10 = _p10 - ray.Origin;

        double a = q00.Cross(ray.Direction).Dot(e00);
        double c = normal.Dot(ray.Direction);
        double b = q10.Cross(ray.Direction).Dot(e11) - (a + c);
        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return;

        double root = Math.Sqrt(discriminant);
        double u1;
        double u2;

        // With c at nought the quadratic is really a linear equation, which is what a ray meeting
        // a patch whose corners are all in one plane gives.  Solving it as a quadratic anyway
        // would divide by that nought; there is only the one crossing, so the second root is put
        // out of range rather than computed.
        if (c.Near(0))
        {
            u1 = b.Near(0) ? -1 : -a / b;
            u2 = -1;
        }
        else
        {
            // The root of larger magnitude is taken first and the other from the product of the
            // two, which is the usual way of keeping the small root's precision: subtracting two
            // nearly equal numbers is what loses it.
            u1 = (-b - Math.CopySign(root, b)) / 2;
            u2 = a / u1;
            u1 /= c;
        }

        AddIntersectionAt(ray, intersections, u1, q00, q10, e00, e11);
        AddIntersectionAt(ray, intersections, u2, q00, q10, e00, e11);
    }

    /// <summary>
    /// This method works out where along the ray a given value of "u" puts the crossing, and adds
    /// it if it lands on the patch.
    /// <para>
    /// At a fixed u the patch is a straight line -- one of the two rulings it is made of -- running
    /// from a point on the P00/P10 edge to the matching point on the P01/P11 edge.  So what is left
    /// is where a ray meets a line segment, which is settled by taking the ray and that segment's
    /// direction together and reading off both parameters at once.
    /// </para>
    /// <para>
    /// The distance is added whatever its sign.  A crossing behind the ray's origin still has to be
    /// reported, or a patch cannot be seen through a refracting surface or used in a CSG.
    /// </para>
    /// </summary>
    private void AddIntersectionAt(
        Ray ray, List<Intersection> intersections, double u,
        Vector q00, Vector q10, Vector e00, Vector e11)
    {
        if (u is < 0 or > 1)
            return;

        Vector along = q00 + (q10 - q00) * u;
        Vector ruling = e00 + (e11 - e00) * u;
        Vector across = ray.Direction.Cross(ruling);
        double scale = across.Dot(across);

        // The ray runs along the ruling itself, so it either misses or lies in it; either way there
        // is no one crossing to name.
        if ((scale * scale).IsNegligibleSquaredBeside(
                ray.Direction.Dot(ray.Direction) * ruling.Dot(ruling)))
            return;

        Vector solved = across.Cross(along);
        double v = solved.Dot(ray.Direction) / scale;

        if (v is < 0 or > 1)
            return;

        double distance = solved.Dot(ruling) / scale;

        intersections.Add(new PrecomputedNormalIntersection(this, distance, NormalAt(u, v)));
    }

    /// <summary>
    /// This method works out the normal at a point on the patch, named by the two parameters
    /// rather than by a position, since that is what the crossing already knows.  The patch runs
    /// straight along each parameter, so the two tangents are the differences of the corners and
    /// the normal is across them both.
    /// </summary>
    /// <param name="u">How far along the patch's first direction the point lies.</param>
    /// <param name="v">How far along its second the point lies.</param>
    /// <returns>The normal to the patch there.</returns>
    private Vector NormalAt(double u, double v)
    {
        Vector alongU = (_p10 - _p00) * (1 - v) + (_p11 - _p01) * v;
        Vector alongV = (_p01 - _p00) * (1 - u) + (_p11 - _p10) * u;

        return alongU.Cross(alongV).Unit;
    }

    /// <summary>
    /// This method returns the normal for the patch.  Every crossing carries the normal worked out
    /// where it happened, so there is nothing to recompute here.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return intersection is PrecomputedNormalIntersection precomputed
            ? precomputed.PrecomputedNormal
            : Directions.Up;
    }
}
