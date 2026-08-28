using RayTracer.Basics;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This is the base class for a surface that is used to render a general path segment for
/// an extrusion.
/// </summary>
public class ExtrusionPathSurface
{
    private readonly IPathSegment _segment;
    private readonly double _minimumY;
    private readonly double _maximumY;

    public ExtrusionPathSurface(IPathSegment segment, double minimumY, double maximumY)
    {
        _segment = segment;
        _minimumY = minimumY;
        _maximumY = maximumY;
    }

    /// <summary>
    /// This method is used to locate the intersection points, if any, where the given ray
    /// intersects this path surface.
    /// </summary>
    /// <param name="ray">The 3D ray we started with.</param>
    /// <param name="projectedRay">The 2D ray to test.</param>
    /// <returns>An array of the intersection data.
    /// If the ray doesn't intersect the surface, the enumerable must be empty.</returns>
    internal IEnumerable<TwoDIntersection> GetTwoDIntersections(Ray ray, TwoDRay projectedRay)
    {
        return _segment.GetIntersections(projectedRay)
            .Select(intersection => AdjustDistance(ray, intersection));
    }

    /// <summary>
    /// This method is used to determine the 3D distance along the ray to the intersection
    /// point.
    /// If the resulting 3D intersection point misses the surface because its Y is too high
    /// or too low, then <c>null</c> will be returned.
    /// </summary>
    /// <param name="ray">The ray currently in play.</param>
    /// <param name="intersection">The 2D intersection to update the distance for.</param>
    /// <returns>The updated intersection or <c>null</c>.</returns>
    private TwoDIntersection AdjustDistance(Ray ray, TwoDIntersection intersection)
    {
        double t3d = GetRayDistance(ray, intersection.Point);

        if (double.IsNaN(t3d))
            return null;

        intersection.Distance = t3d;

        return intersection;
    }

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
    private double GetRayDistance(Ray ray, TwoDPoint point)
    {
        // The ray is projected on to the X/Z plane, so it can be followed along whichever of the
        // two axes it travels faster in -- and only a ray straight up or down travels along
        // neither.  Both tests are made against the ray's own direction, since a scaled-up
        // extrusion shrinks the direction it is carried in with and would otherwise call an
        // ordinary slanted ray vertical.  Dividing by the larger component also keeps the most
        // precision, where taking X whenever it was not quite zero threw some away.
        double directionX = ray.Direction.X;
        double directionZ = ray.Direction.Z;
        double directionSquared = ray.Direction.Dot(ray.Direction);
        bool useX = Math.Abs(directionX) >= Math.Abs(directionZ);
        double larger = useX ? directionX : directionZ;

        if ((larger * larger).IsNegligibleSquaredBeside(directionSquared))
            return double.NaN;

        double distance = useX
            ? (point.X - ray.Origin.X) / directionX
            : (point.Y - ray.Origin.Z) / directionZ;
        double y = ray.Origin.Y + distance * ray.Direction.Y;

        return y < _minimumY || y > _maximumY ? double.NaN : distance;
    }
}
