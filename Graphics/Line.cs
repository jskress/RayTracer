using RayTracer.Basics;
using RayTracer.Geometry;

namespace RayTracer.Graphics;

/// <summary>
/// This class provides a representation of a line, providing the common math we need.
/// </summary>
public class Line : IPathSegment
{
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
}
