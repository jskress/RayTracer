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

    /// <summary>
    /// This method counts how many times this segment crosses the horizontal line through the
    /// given point, strictly to its right.  It answers the same question
    /// <see cref="GetIntersections"/> would for a rightward horizontal ray, but returns only the
    /// count -- no ray, no intersection objects, no collection to hand back.  That matters
    /// because <see cref="GeneralPath.Contains"/> calls this for every ray that reaches a flat
    /// surface's plane and wants nothing but the tally, so an implementation must not allocate.
    /// <para>
    /// An implementation must first check whether its own defining points -- for a line, its two
    /// endpoints; for a curve, its endpoints *and* its control points -- are all on the same side
    /// of the point's Y, comparing with a strict "greater than" on one side and "at or below" on
    /// the other, the usual even/odd tie-breaker.  If they are, it must report no crossings.  A
    /// Bezier curve never leaves the convex hull of its own defining points, so if every one of
    /// them is on the same side, the curve provably cannot reach the test line.  Testing only the
    /// two endpoints would be wrong for a curve: a segment whose endpoints sit on the same side
    /// can still bulge across the test line and back through its interior, and those crossings
    /// have to be counted rather than skipped.  The skip is not merely an optimisation either --
    /// without it, two segments that only touch the test line at a shared vertex, without the
    /// path actually crossing from one side to the other there (a flat-topped notch, say), would
    /// each report that shared point as a crossing of its own, counting one non-crossing touch
    /// twice.
    /// </para>
    /// </summary>
    /// <param name="point">The point whose horizontal line is to be crossed.</param>
    /// <returns>The number of crossings strictly to the right of the point.</returns>
    int CountCrossingsToTheRight(TwoDPoint point);
}
