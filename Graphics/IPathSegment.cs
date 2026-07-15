namespace RayTracer.Graphics;

/// <summary>
/// This interface defines the contract that a path segment (2D element) must follow to support
/// intersection testing. 
/// </summary>
public interface IPathSegment
{
    /// <summary>
    /// This property exposes the points that define this segment.
    /// </summary>
    TwoDPoint[] Points { get; }

    /// <summary>
    /// This method is used to reverse the direction of this path segment.
    /// </summary>
    void Reverse();

    /// <summary>
    /// This method returns the point on this segment at the given parameter.
    /// </summary>
    /// <param name="t">The parameter to evaluate at, from 0 (the segment's start) to 1 (its
    /// end).</param>
    /// <returns>The point at that parameter.</returns>
    TwoDPoint GetPoint(double t);

    /// <summary>
    /// This method is used to locate the intersection points, if any, where the given ray
    /// intersects this bit of geometry.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>An array of the intersection data.
    /// If the ray doesn't intersect this bit of geometry, the enumerable must be empty.</returns>
    IEnumerable<TwoDIntersection> GetIntersections(TwoDRay ray);
}
