using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Graphics;

/// <summary>
/// This class provides a representation of a line, providing the common math we need.
/// </summary>
public class Line : IPathSegment
{
    /// <summary>
    /// This property exposes the points that define this segment.
    /// </summary>
    public TwoDPoint[] Points => [_start, _end];

    /// <summary>
    /// This property exposes this line's constant 2D normal vector.
    /// </summary>
    public TwoDVector Normal => _normal;

    private TwoDPoint _start;
    private TwoDPoint _end;
    private Parallelogram _parallelogram;
    private TwoDVector _normal;

    internal Line(TwoDPoint start, TwoDPoint end)
    {
        SetPoints(start, end);
    }

    /// <summary>
    /// This method is used to set up our coefficients based on the given control points.
    /// </summary>
    /// <param name="start">The point at which the curve starts.</param>
    /// <param name="end">The point at which the curve ends.</param>
    private void SetPoints(TwoDPoint start, TwoDPoint end)
    {
        Point point0 = new Point(start.X, -0.5, start.Y);
        Point point1 = new Point(end.X, -0.5, end.Y);
        Point point2 = new Point(start.X, 0.5, start.Y);

        _start = start;
        _end = end;
        _parallelogram = new Parallelogram
        {
            Point = point0,
            Side1 = point1 - point0,
            Side2 = point2 - point0
        };

        _normal = TwoDVector.ProjectedToXz(_parallelogram.Normal).Unit;
    }

    /// <summary>
    /// This method is used to reverse the direction of this path segment.
    /// </summary>
    public void Reverse()
    {
        SetPoints(_end, _start);
    }

    /// <summary>
    /// This method returns the point on this line at the given parameter.
    /// </summary>
    /// <param name="t">The parameter to evaluate at, from 0 (the line's start) to 1 (its
    /// end).</param>
    /// <returns>The point at that parameter.</returns>
    public TwoDPoint GetPoint(double t)
    {
        return _start + (_end - _start) * t;
    }

    /// <summary>
    /// This method is used to locate the intersection points, if any, where the given ray
    /// intersects this line.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>An array of the intersection data.
    /// If the ray doesn't intersect the line, the enumerable must be empty.</returns>
    public IEnumerable<TwoDIntersection> GetIntersections(TwoDRay ray)
    {
        double t = _parallelogram.GetIntersection(ray.FromXz());

        return double.IsNaN(t)
            ? []
            :
            [
                new TwoDIntersection
                {
                    Distance = t,
                    Point = ray.At(t),
                    TwoDNormal = _normal
                }
            ];
    }

    /// <summary>
    /// This method counts how many times this line crosses the horizontal line through the given
    /// point, strictly to its right.  See <see cref="IPathSegment.CountCrossingsToTheRight"/> for
    /// what the count is for and why the straddle test comes first.
    /// <para>
    /// This solves the crossing in plain 2D arithmetic rather than going through the parallelogram
    /// <see cref="GetIntersections"/> uses.  That route asks a 3D surface where a 3D ray meets it,
    /// which for a segment extruded into a vertical quad is a great deal of machinery -- and,
    /// since points and vectors here are reference types, a great deal of allocation -- to answer
    /// what is really "where does this segment cross a horizontal line".  The answer is the same:
    /// the segment's own parameter must land within it, exactly as the parallelogram requires its
    /// alpha to fall in [0, 1].
    /// </para>
    /// </summary>
    /// <param name="point">The point whose horizontal line is to be crossed.</param>
    /// <returns>The number of crossings strictly to the right of the point.</returns>
    public int CountCrossingsToTheRight(TwoDPoint point)
    {
        bool startAbove = _start.Y > point.Y;

        // Both endpoints on the same side, so the line cannot reach the test line.
        if (startAbove == _end.Y > point.Y)
            return 0;

        // The straddle above rules out a horizontal line, so this cannot divide by zero.
        double alpha = (point.Y - _start.Y) / (_end.Y - _start.Y);

        if (alpha is < 0 or > 1)
            return 0;

        return _start.X + alpha * (_end.X - _start.X) > point.X ? 1 : 0;
    }
}
