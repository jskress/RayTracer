using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for a surface that is used to render a general path segment.
/// It is assumed that the path segment is on the X/Z plane.
/// </summary>
public abstract class PathSurface
{
    /// <summary>
    /// This holds the low end of the surface
    /// </summary>
    protected double MinimumY { get; }

    /// <summary>
    /// This holds the high end of the surface
    /// </summary>
    protected double MaximumY { get; }

    protected PathSurface(double minimumY, double maximumY)
    {
        MinimumY = minimumY;
        MaximumY = maximumY;
    }

    /// <summary>
    /// This method is used to locate the intersection point, if any, where the given ray
    /// intersects this path surface.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>An array of the intersection distance and normal vector pairs.
    /// If the ray doesn't intersect the surface, the array will be <c>null</c>.</returns>
    public abstract SimpleIntersection[] GetIntersection(Ray ray);

    /// <summary>
    /// This is a helper method that will take a point on the X/Z plane and projects it
    /// up to the given ray to find the distance from the ray's origin to the point of
    /// intersection.
    /// If the intersection occurs either above or below the surface, then <c>NaN</c>
    /// will be returned. 
    /// </summary>
    /// <param name="ray">The ray along which the distance is required.</param>
    /// <param name="point">The point at which the ray, projected on to the X/Z plane,
    /// intersects the surface.</param>
    /// <returns>The distance along the ray where the intersection happens, or <c>NaN</c>,
    /// if the intersection is too low or too high for this surface.</returns>
    protected double GetRayDistance(Ray ray, TwoDPoint point)
    {
         double distance = (point.X - ray.Origin.X) / ray.Direction.X;
         double y = ray.Origin.Y + distance * ray.Direction.Y;
         
         return y < MinimumY || y > MaximumY ? double.NaN : distance;
    }
}
