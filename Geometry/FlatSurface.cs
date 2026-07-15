using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for surfaces that are flat: a single plane, with some 2D test
/// deciding which points of that plane are actually part of the surface.  It owns the
/// ray/plane intersection math (including rejecting hits behind the ray's origin, which
/// every flat shape agrees on), leaving subclasses to decide only whether a given point on
/// the plane is actually inside their shape.
/// </summary>
public abstract class FlatSurface : Surface
{
    /// <summary>
    /// This property holds the surface's normal, which is constant across its whole plane.
    /// </summary>
    public Vector Normal { get; protected set; }

    /// <summary>
    /// This property holds the constant, <c>d</c>, in the surface's plane equation,
    /// <c>Normal . P = d</c>, for any point, <c>P</c>, on the plane.
    /// </summary>
    protected double PlaneConstant { get; set; }

    /// <summary>
    /// This method is used to determine whether the given ray intersects the surface's
    /// plane and, if so, whether that point also lies within the shape itself.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <param name="intersections">The list to add any intersections to.</param>
    public override void AddIntersections(Ray ray, List<Intersection> intersections)
    {
        double? t = GetPlaneDistance(ray);

        if (t is null)
            return;

        Intersection intersection = TryCreateIntersection(ray.At(t.Value), t.Value);

        if (intersection is not null)
            intersections.Add(intersection);
    }

    /// <summary>
    /// This method finds the distance, along the given ray, to this surface's plane,
    /// rejecting rays parallel to the plane and hits behind the ray's origin.  It's exposed
    /// (rather than folded entirely into <see cref="AddIntersections"/>) so that subclasses
    /// needing just the raw plane-hit distance, without allocating an intersection list, can
    /// reuse the same math -- <see cref="Parallelogram"/>'s <c>GetIntersection</c> is used
    /// this way by <see cref="Extrusion"/>'s end-cap testing.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>The distance to the plane, or <c>null</c> if the ray is parallel to the
    /// plane or the plane is behind the ray's origin.</returns>
    protected double? GetPlaneDistance(Ray ray)
    {
        double denominator = Normal.Dot(ray.Direction);

        if (denominator.Near(0))
            return null;

        double t = (PlaneConstant - Normal.Dot(ray.Origin)) / denominator;

        return t < 0 ? null : t;
    }

    /// <summary>
    /// This method is used to test whether the given point, which is already known to lie
    /// on the surface's plane, actually lies within the shape.
    /// </summary>
    /// <param name="point">The point, on the surface's plane, to test.</param>
    /// <param name="distance">The distance along the ray where the point lies.</param>
    /// <returns>The appropriate intersection object, or <c>null</c> if the point is not
    /// actually part of the shape.</returns>
    protected abstract Intersection TryCreateIntersection(Point point, double distance);

    /// <summary>
    /// This method returns the normal for the surface.  It is assumed that the point will
    /// have been transformed to surface-space coordinates.  The vector returned will
    /// also be in surface-space coordinates.
    /// </summary>
    /// <param name="point">The point at which the normal should be determined.</param>
    /// <param name="intersection">The intersection information.</param>
    /// <returns>The normal to the surface at the given point.</returns>
    public override Vector SurfaceNormaAt(Point point, Intersection intersection)
    {
        return Normal;
    }
}
