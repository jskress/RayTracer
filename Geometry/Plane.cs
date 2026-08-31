using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

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
        // A ray misses the plane only when it runs along it, and that is a question about the
        // ray's angle, not about the size of its Y component -- which shrinks with the plane's
        // own scale.  Judged absolutely, a scaled-up floor lost its horizon.
        double directionY = ray.Direction.Y;

        if ((directionY * directionY).IsNegligibleSquaredBeside(ray.Direction.Dot(ray.Direction)))
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
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormalAt(Point point, Intersection intersection)
    {
        return Directions.Up;
    }
}
