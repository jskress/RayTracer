using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a conic (double-mapped cone).  It is defined as centered at the
/// origin, with its apex at the origin and extends along the Y axis.  It may be stopped
/// or capped.
/// </summary>
public class Conic : CircularSurface
{
    /// <summary>
    /// This method is used to determine whether the given ray intersects the conic and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double a = ray.Direction.X * ray.Direction.X - ray.Direction.Y * ray.Direction.Y +
                   ray.Direction.Z * ray.Direction.Z;
        double b = ray.Origin.X * ray.Direction.X * 2 -
                   ray.Origin.Y * ray.Direction.Y * 2 +
                   ray.Origin.Z * ray.Direction.Z * 2;
        double c = ray.Origin.X * ray.Origin.X - ray.Origin.Y * ray.Origin.Y +
                   ray.Origin.Z * ray.Origin.Z;
        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return;

        if (a.Near(0) && !b.Near(0))
            intersections.Add(new Intersection(this, -c / (2 * b)));
        else if (!a.Near(0))
        {
            a *= 2;
            b = -b;

            double disc = Math.Sqrt(discriminant);
            double t0 = (b - disc) / a;
            double t1 = (b + disc) / a;

            if (t0 > t1)
                (t0, t1) = (t1, t0);

            double y0 = ray.Origin.Y + t0 * ray.Direction.Y;
            double y1 = ray.Origin.Y + t1 * ray.Direction.Y;

            if (y0 > MinimumY && y0 < MaximumY)
                intersections.Add(new Intersection(this, t0));

            if (y1 > MinimumY && y1 < MaximumY)
                intersections.Add(new Intersection(this, t1));
        }

        if (Closed && !ray.Direction.Y.Near(0))
            AddCappedIntersections(ray, intersections);
    }

    /// <summary>
    /// This method tests for end cap intersections, if they are defined.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    private void AddCappedIntersections(Ray ray, List<Intersection> intersections)
    {
        double t;

        if (!double.IsNegativeInfinity(MinimumY))
        {
            t = (MinimumY - ray.Origin.Y) / ray.Direction.Y;

            if (CheckCap(ray, t, MinimumY))
                intersections.Add(new Intersection(this, t));
        }

        if (!double.IsPositiveInfinity(MaximumY))
        {
            t = (MaximumY - ray.Origin.Y) / ray.Direction.Y;

            if (CheckCap(ray, t, MaximumY))
                intersections.Add(new Intersection(this, t));
        }
    }

    /// <summary>
    /// This is a helper method to check whether a cap is intersected at a particular place.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="t">The value to check.</param>
    /// <returns><c>true</c>, if the cap is intersected, or <c>false</c>, if not.</returns>
    private static bool CheckCap(Ray ray, double t, double y)
    {
        double x = ray.Origin.X + t * ray.Direction.X;
        double z = ray.Origin.Z + t * ray.Direction.Z;

        return x * x + z * z <= Math.Abs(y);
    }

    /// <summary>
    /// This method returns the normal for the conic.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point)
    {
        double distance = point.X * point.X + point.Z * point.Z;

        if (distance < 1 && point.Y >= MaximumY - DoubleExtensions.Epsilon)
            return Directions.Up;

        if (distance < 1 && point.Y <= MinimumY + DoubleExtensions.Epsilon)
            return Directions.Down;

        double y = Math.Sqrt(distance);

        if (point.Y > 0)
            y = -y;

        return new Vector(point.X, y, point.Z);
    }
}
