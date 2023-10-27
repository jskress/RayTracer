using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a cube.  It is defined as centered at the origin and extends from
/// <c>-1</c> to <c>1</c> along each axis.
/// </summary>
public class Cube : Surface
{
    /// <summary>
    /// This holds a bounding box that aligns with our own shape.  Our ray/intersection
    /// stuff is delegated to this.
    /// </summary>
    private static readonly BoundingBox BoundingBox = new BoundingBox(
        new Point(-1, -1, -1), new Point(1, 1, 1));

    /// <summary>
    /// This method is used to determine whether the given ray intersects the cube and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        (double tMin, double tMax) = BoundingBox.GetIntersections(ray);

        if (tMin <= tMax)
        {
            intersections.Add(new Intersection(this, tMin));
            intersections.Add(new Intersection(this, tMax));
        }
    }

    /// <summary>
    /// This method returns the normal for the cube.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
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
