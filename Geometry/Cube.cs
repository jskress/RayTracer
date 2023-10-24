using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a cube.  It is defined as centered at the origin and extens from
/// <c>-1</c> to <c>1</c> along each axis.
/// </summary>
public class Cube : Surface
{
    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        (double xMin, double xMax) = CheckAxis(ray.Origin.X, ray.Direction.X);
        (double yMin, double yMax) = CheckAxis(ray.Origin.Y, ray.Direction.Y);
        (double zMin, double zMax) = CheckAxis(ray.Origin.Z, ray.Direction.Z);
        double tMin = Math.Max(xMin, Math.Max(yMin, zMin));
        double tMax = Math.Min(xMax, Math.Min(yMax, zMax));

        if (tMin <= tMax)
        {
            intersections.Add(new Intersection(this, tMin));
            intersections.Add(new Intersection(this, tMax));
        }
    }

    /// <summary>
    /// This method handles finding intersection points for a specific pair of axis planes.
    /// </summary>
    /// <param name="origin">The origin value for the axis.</param>
    /// <param name="direction">The direction value for the axis.</param>
    /// <returns>The min and max intersection points for the axis being tested..</returns>
    private (double min, double max) CheckAxis(double origin, double direction)
    {
        double minNumerator = -1 - origin;
        double maxNumerator = 1 - origin;
        double min;
        double max;

        if (Math.Abs(direction) >= DoubleExtensions.Epsilon)
        {
            min = minNumerator / direction;
            max = maxNumerator / direction;
        }
        else
        {
            min = minNumerator * double.PositiveInfinity;
            max = maxNumerator * double.PositiveInfinity;
        }

        if (min > max)
            (min, max) = (max, min);

        return (min, max);
    }

    /// <summary>
    /// This method returns the normal for the cube.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point)
    {
        double x = Math.Abs(point.X);
        double y = Math.Abs(point.Y);
        double z = Math.Abs(point.Z);
        double max = Math.Max(x, Math.Max(y, z));

        if (max.Near(x))
            return new Vector(point.X, 0, 0);

        return max.Near(y)
            ? new Vector(0, point.Y, 0)
            : new Vector(0, 0, point.Z);
    }
}
