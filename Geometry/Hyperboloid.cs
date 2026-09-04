using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a hyperboloid of one sheet: the waisted surface where
/// <c>x² + z² - y² = 1</c>, narrowest at the origin and flaring away above and below it.  It may be
/// stopped or capped, as a cylinder or a conic is.
/// <para>
/// **It takes no numbers of its own.**  Scaling X and Z by <c>a</c> and Y by <c>b</c> gives every
/// waist and every flare there is, so the unit form plus a transform covers the family — the same
/// reason a cylinder needs no radius.
/// </para>
/// <para>
/// **What it is for**: a cooling tower is this shape and so is a waisted stool, a wastepaper basket
/// and the hourglass-ish middle of a turned baluster.  It is also the shape a straight line sweeps
/// when it is spun about an axis it does not meet, which is why concrete ones are built from straight
/// reinforcing bars.
/// </para>
/// <para>
/// Only the one-sheet form is offered here.  The two-sheet hyperboloid -- a pair of opposed bowls,
/// <c>x² + z² - y² = -1</c> -- is a rare thing in a scene and is available from
/// <see cref="Quadric"/> for anyone who wants it.
/// </para>
/// </summary>
public class Hyperboloid : ExtrudedSurface
{
    /// <summary>
    /// This method returns the box the surface sits in.
    /// <para>
    /// Its radius at a height is <c>sqrt(1 + y²)</c>, so the widest it gets is at whichever end lies
    /// further from the waist.  One that runs on forever has no box.
    /// </para>
    /// </summary>
    /// <returns>The box this hyperboloid sits in, or <c>null</c> if it has no ends.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        if (double.IsInfinity(MinimumY) || double.IsInfinity(MaximumY))
            return null;

        double reach = Math.Max(Math.Abs(MinimumY), Math.Abs(MaximumY));
        double widest = Math.Sqrt(1 + reach * reach);

        return new BoundingBox()
            .Add(new Point(-widest, MinimumY, -widest))
            .Add(new Point(widest, MaximumY, widest));
    }

    /// <summary>
    /// This method finds where a ray crosses the surface.
    /// <para>
    /// **The degenerate ray here is the one running along an asymptote**, at forty-five degrees to the
    /// axis, where the squared term falls out because the ray keeps pace with the flare exactly.  Such
    /// a ray still meets the surface once -- it is climbing away from the waist as fast as the surface
    /// widens, so it crosses the far side and never comes back -- and the equation left is a straight
    /// line.  This is the same trap the bowl has and a different geometry for it.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double a = ray.Direction.X * ray.Direction.X + ray.Direction.Z * ray.Direction.Z -
                   ray.Direction.Y * ray.Direction.Y;
        double b = 2 * (ray.Origin.X * ray.Direction.X + ray.Origin.Z * ray.Direction.Z -
                        ray.Origin.Y * ray.Direction.Y);
        double c = ray.Origin.X * ray.Origin.X + ray.Origin.Z * ray.Origin.Z -
                   ray.Origin.Y * ray.Origin.Y - 1;
        double directionSquared = ray.Direction.Dot(ray.Direction);

        if (a.IsNegligibleSquaredBeside(directionSquared))
        {
            if (!(b * b).IsNegligibleSquaredBeside(directionSquared))
                AddIfWithinHeight(ray, -c / b, intersections);
        }
        else
        {
            double discriminant = b * b - 4 * a * c;

            if (discriminant < 0)
                return;

            double root = Math.Sqrt(discriminant);
            double t0 = (-b - root) / (2 * a);
            double t1 = (-b + root) / (2 * a);

            if (t0 > t1)
                (t0, t1) = (t1, t0);

            AddIfWithinHeight(ray, t0, intersections);
            AddIfWithinHeight(ray, t1, intersections);
        }

        double directionY = ray.Direction.Y;

        if (Closed && !(directionY * directionY).IsNegligibleSquaredBeside(directionSquared))
            AddCapIntersections(ray, intersections);
    }

    /// <summary>
    /// This method records a crossing of the wall, if it lies between the ends.  The ends are left
    /// out, as a cylinder's are: here the caps really do cover their rims, the radius of an end being
    /// a genuine circle rather than the bowl's single point.
    /// </summary>
    private void AddIfWithinHeight(Ray ray, double distance, List<Intersection> intersections)
    {
        double y = ray.Origin.Y + distance * ray.Direction.Y;

        if (y > MinimumY && y < MaximumY)
            intersections.Add(new Intersection(this, distance));
    }

    /// <summary>
    /// This method tests the flat ends, whose radius at a height is <c>sqrt(1 + y²)</c>.
    /// </summary>
    private void AddCapIntersections(Ray ray, List<Intersection> intersections)
    {
        foreach (double height in new[] { MinimumY, MaximumY })
        {
            if (double.IsInfinity(height))
                continue;

            double distance = (height - ray.Origin.Y) / ray.Direction.Y;
            double x = ray.Origin.X + distance * ray.Direction.X;
            double z = ray.Origin.Z + distance * ray.Direction.Z;

            if (x * x + z * z <= 1 + height * height)
                intersections.Add(new Intersection(this, distance));
        }
    }

    /// <summary>
    /// This method returns the normal, which is the slope of <c>x² + z² - y²</c>.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        if (Closed && !double.IsInfinity(MaximumY) && point.Y >= MaximumY - DoubleExtensions.Epsilon)
            return new Vector(0, 1, 0);

        if (Closed && !double.IsInfinity(MinimumY) && point.Y <= MinimumY + DoubleExtensions.Epsilon)
            return new Vector(0, -1, 0);

        return new Vector(2 * point.X, -2 * point.Y, 2 * point.Z);
    }
}
