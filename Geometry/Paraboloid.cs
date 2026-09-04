using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a paraboloid: the bowl where <c>x² + z² = y</c>, opening along the positive
/// Y axis with its nose at the origin.  It may be stopped or capped, as a cylinder or a conic is.
/// <para>
/// **It takes no numbers of its own**, for the same reason a cylinder does not: scaling covers the
/// whole family.  Scaling X and Z by <c>a</c> and Y by <c>b</c> turns the unit bowl into
/// <c>x² + z² = (a²/b)·y</c>, so any opening rate is a transform away.  A torus needs its two radii
/// because no scaling produces a different ratio between them; a paraboloid has no such number.
/// </para>
/// <para>
/// **What it is for**: a paraboloid gathers everything arriving along its axis to one point, so a dish,
/// a headlamp reflector, a telescope mirror and a solar collector are all this shape and behave like
/// it once the surface is reflective.  It is also the surface a spun liquid settles into.
/// </para>
/// </summary>
public class Paraboloid : ExtrudedSurface
{
    /// <summary>
    /// A bowl starts at its nose rather than at minus one, which is where the family's other members
    /// start: nothing of the surface exists below nought, so the usual default would name a stretch
    /// that is not there.
    /// </summary>
    public Paraboloid()
    {
        MinimumY = 0;
    }

    /// <summary>
    /// This method returns the box the bowl sits in.
    /// <para>
    /// Its radius at a height is the square root of that height, so the widest it gets is at its top;
    /// the bottom is the narrow end.  A bowl with no top has no box.
    /// </para>
    /// </summary>
    /// <returns>The box this paraboloid sits in, or <c>null</c> if it has no top.</returns>
    protected override BoundingBox GetDefaultBoundingBox()
    {
        if (double.IsInfinity(MinimumY) || double.IsInfinity(MaximumY))
            return null;

        // Nothing of the surface exists below y = 0, so a bowl told to start lower starts at nought.
        double bottom = Math.Max(0, MinimumY);
        double top = Math.Max(bottom, MaximumY);
        double widest = Math.Sqrt(top);

        return new BoundingBox()
            .Add(new Point(-widest, bottom, -widest))
            .Add(new Point(widest, top, widest));
    }

    /// <summary>
    /// This method finds where a ray crosses the bowl.
    /// <para>
    /// **The ray running straight down the axis is a hit here, not a miss**, and that is where this
    /// parts company with the cylinder it otherwise resembles.  A ray parallel to a cylinder's wall
    /// never meets it, so the cylinder simply gives up when the squared term vanishes.  A paraboloid
    /// is closed at the bottom: a ray down its axis meets the nose, and the equation left when the
    /// squared term goes is a straight line with one root.  Dropping that case would put a hole in
    /// the middle of every dish seen face on.
    /// </para>
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double a = ray.Direction.X * ray.Direction.X + ray.Direction.Z * ray.Direction.Z;
        double b = 2 * (ray.Origin.X * ray.Direction.X + ray.Origin.Z * ray.Direction.Z) -
                   ray.Direction.Y;
        double c = ray.Origin.X * ray.Origin.X + ray.Origin.Z * ray.Origin.Z - ray.Origin.Y;

        // Judged against the ray's own direction rather than a fixed number: a world ray carried into
        // the space of a scaled-up bowl is merely short, and an absolute test would call it axial.
        double directionSquared = ray.Direction.Dot(ray.Direction);

        if (a.IsNegligibleSquaredBeside(directionSquared))
        {
            // Down the axis: one crossing, where the line meets the nose.
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
    /// This method records a crossing of the bowl itself, if it lies between the ends.
    /// </summary>
    private void AddIfWithinHeight(Ray ray, double distance, List<Intersection> intersections)
    {
        double y = ray.Origin.Y + distance * ray.Direction.Y;

        // **The ends are included, where a cylinder's are not**, and the nose is why.  A cylinder
        // leaves its rims to its caps, which cover them exactly; a bowl's bottom cap has a radius of
        // the square root of its height, so at the nose it is a single point and covers nothing.  Left
        // out, an open bowl loses its nose and a closed one keeps it only by the accident of a cap
        // test that admits one point.  A ray meeting a rim now finds the wall and the cap at the same
        // distance, which is what a tangency is and what CSG already knows how to read.
        if (y >= MinimumY && y <= MaximumY)
            intersections.Add(new Intersection(this, distance));
    }

    /// <summary>
    /// This method tests the flat ends, whose radius is the square root of their own height.
    /// </summary>
    private void AddCapIntersections(Ray ray, List<Intersection> intersections)
    {
        foreach (double height in new[] { MinimumY, MaximumY })
        {
            if (double.IsInfinity(height) || height < 0)
                continue;

            double distance = (height - ray.Origin.Y) / ray.Direction.Y;
            double x = ray.Origin.X + distance * ray.Direction.X;
            double z = ray.Origin.Z + distance * ray.Direction.Z;

            if (x * x + z * z <= height)
                intersections.Add(new Intersection(this, distance));
        }
    }

    /// <summary>
    /// This method returns the normal to the bowl, which is the slope of <c>x² + z² - y</c>.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        // On a flat end, the end's own normal; the walls meet the ends at a corner, so which one a
        // point belongs to is decided by how close it is to the plane of an end.
        if (Closed && !double.IsInfinity(MaximumY) && point.Y >= MaximumY - DoubleExtensions.Epsilon)
            return new Vector(0, 1, 0);

        if (Closed && !double.IsInfinity(MinimumY) && point.Y <= MinimumY + DoubleExtensions.Epsilon)
            return new Vector(0, -1, 0);

        return new Vector(2 * point.X, -1, 2 * point.Z);
    }
}
