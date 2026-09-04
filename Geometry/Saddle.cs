using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a saddle -- a hyperbolic paraboloid -- the sheet where <c>y = x² - z²</c>,
/// rising along X and falling along Z, cut to a rectangle about the origin.
/// <para>
/// **It is a sheet and not a solid**, and that follows from cutting it to a rectangle.  Left endless
/// it would divide space in two and be a half-space, exactly as a <see cref="Plane"/> is; bounded, it
/// is a finite four-cornered surface with nothing inside it, exactly as a
/// <see cref="BicubicPatch"/> or a parallelogram is.  Anyone wanting the endless, solid form for a
/// difference or an intersection has it from <see cref="Quadric"/>.
/// </para>
/// <para>
/// **It takes no shape numbers**, only its extent: scaling X and Z against Y gives every pair of
/// curvatures there is.
/// </para>
/// <para>
/// **What it is for**: this is the roof shell -- the doubly-curved concrete or timber roof that reads
/// as modern the moment it appears -- and it is built from straight members in both directions, since
/// the surface is doubly ruled.  A crisp packet is one, and so is the seat of a saddle.
/// </para>
/// </summary>
public class Saddle : Surface
{
    /// <summary>
    /// This surface is a sheet: it encloses nothing, so its normal points whichever way it was
    /// written rather than naming an outside.  See <see cref="Surface.IsASheet"/>.
    /// </summary>
    public override bool IsASheet => true;

    /// <summary>
    /// This property holds how far the sheet reaches along X, in total.
    /// </summary>
    public double Width { get; set; } = 2;

    /// <summary>
    /// This property holds how far the sheet reaches along Z, in total.
    /// </summary>
    public double Depth { get; set; } = 2;

    /// <summary>
    /// This method returns the box the sheet lies in.  Its highest corners are where X is greatest
    /// and Z is nought, and its lowest where the opposite holds.
    /// </summary>
    /// <returns>The box this saddle lies in.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        double halfWidth = Width * 0.5;
        double halfDepth = Depth * 0.5;

        return new BoundingBox()
            .Add(new Point(-halfWidth, -halfDepth * halfDepth, -halfDepth))
            .Add(new Point(halfWidth, halfWidth * halfWidth, halfDepth));
    }

    /// <summary>
    /// This method finds where a ray crosses the sheet.
    /// <para>
    /// **The degenerate ray here runs at forty-five degrees in the X/Z plane**, along one of the two
    /// straight lines the surface is ruled by: there the squared terms cancel exactly, because the
    /// rise along X is matched by the fall along Z.  Such a ray meets the sheet once, where the
    /// remaining straight line says, and refusing it would draw two blank diagonals across every
    /// saddle.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double a = ray.Direction.X * ray.Direction.X - ray.Direction.Z * ray.Direction.Z;
        double b = 2 * (ray.Origin.X * ray.Direction.X - ray.Origin.Z * ray.Direction.Z) -
                   ray.Direction.Y;
        double c = ray.Origin.X * ray.Origin.X - ray.Origin.Z * ray.Origin.Z - ray.Origin.Y;
        double directionSquared = ray.Direction.Dot(ray.Direction);

        if (a.IsNegligibleSquaredBeside(directionSquared))
        {
            if (!(b * b).IsNegligibleSquaredBeside(directionSquared))
                AddIfWithinExtent(ray, -c / b, intersections);

            return;
        }

        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return;

        double root = Math.Sqrt(discriminant);
        double t0 = (-b - root) / (2 * a);
        double t1 = (-b + root) / (2 * a);

        if (t0 > t1)
            (t0, t1) = (t1, t0);

        AddIfWithinExtent(ray, t0, intersections);
        AddIfWithinExtent(ray, t1, intersections);
    }

    /// <summary>
    /// This method records a crossing, if it falls inside the rectangle the sheet is cut to.
    /// </summary>
    private void AddIfWithinExtent(Ray ray, double distance, List<Intersection> intersections)
    {
        double x = ray.Origin.X + distance * ray.Direction.X;
        double z = ray.Origin.Z + distance * ray.Direction.Z;

        if (Math.Abs(x) <= Width * 0.5 && Math.Abs(z) <= Depth * 0.5)
            intersections.Add(new Intersection(this, distance));
    }

    /// <summary>
    /// This method returns the normal, which is the slope of <c>x² - z² - y</c>.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return new Vector(2 * point.X, -1, -2 * point.Z);
    }
}
