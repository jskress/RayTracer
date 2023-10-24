using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents a plane.  By definition, it is the xz plane.
/// </summary>
public class Plane : Surface
{
    /// <summary>
    /// This method is used to determine whether the given ray intersects the plane and,
    /// if so, where.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        if (ray.Direction.Y.Near(0))
            return;

        double distance = -ray.Origin.Y / ray.Direction.Y;

        intersections.Add(new Intersection(this, distance));
    }

    /// <summary>
    /// This method returns the normal for the plane.  Since we are a plane, the normal
    /// is a constant.  It is assumed that the point will have been transformed to
    /// surface-space coordinates.  The vector returned will also be in surface-space
    /// coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point)
    {
        return Directions.Up;
    }
}
