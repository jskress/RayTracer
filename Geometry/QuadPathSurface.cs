using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.Extensions;
using RayTracer.Graphics;

namespace RayTracer.Geometry;

/// <summary>
/// This class represents the surface for a quadratic Bézier curve path segment.
/// </summary>
public class QuadPathSurface : PathSurface
{
    private readonly TwoDPoint _pStart;
    private readonly QuadCurve _curve;
    private readonly double _aX;
    private readonly double _aY;
    private readonly double _bX;
    private readonly double _bY;

    public QuadPathSurface(QuadPathSegment segment, double minimumY, double maximumY)
        : base(minimumY, maximumY)
    {
        TwoDPoint pControl = segment.Points[1];
        TwoDPoint pEnd = segment.Points[2];

        _pStart = segment.Points[0];
        _curve = new QuadCurve(_pStart, pControl, pEnd);

        _aX = _pStart.X - 2 * pControl.X + pEnd.X;
        _aY = _pStart.Y - 2 * pControl.Y + pEnd.Y;
        _bX = _pStart.X - pControl.X;
        _bY = _pStart.Y - pControl.Y;
    }

    /// <summary>
    /// This method is used to locate the intersection point, if any, where the given ray
    /// intersects this path surface.
    /// </summary>
    /// <remarks>
    /// Algorithm from: https://www.tumblr.com/floorplanner-techblog/66681002205/computing-the-intersection-between-linear-and
    /// </remarks>
    /// <param name="ray">The ray to test.</param>
    /// <returns>An array of tuples containing the intersection distance and normal vector
    /// pairs.
    /// If the ray doesn't intersect the surface, the array will be <c>null</c>.</returns>
    public override SimpleIntersection[] GetIntersection(Ray ray)
    {
        Point point = ray.Origin + ray.Direction;
        TwoDPoint lineA = new TwoDPoint(ray.Origin.X, ray.Origin.Z);
        TwoDPoint lineB = new TwoDPoint(point.X, point.Z);
        double a;
        double b;
        double c;

        if (lineA.X.Near(lineB.X))
        {
            a = _aX;
            b = -2 * _bX;
            c = _pStart.X - lineA.X;
        }
        else if (lineA.Y.Near(lineB.Y))
        {
            a = _aY;
            b = -2 * _bY;
            c = _pStart.Y - lineA.Y;
        }
        else
        {
            double k = (lineA.Y - lineB.Y) / (lineB.X - lineA.X);

            a = k * _aX + _aY;
            b = -2 * (k * _bX + _bY);
            c = k * (_pStart.X - lineA.X) + _pStart.Y - lineA.Y;
        }

        return QuadCurve.Evaluate(a, b, c)
            .Select(t => IntersectionDataAt(ray, t))
            .ToArray();
    }

    /// <summary>
    /// This method is used to convert the given 2D intersection distance along the curve
    /// into the appropriate ray distance and normal vector.
    /// </summary>
    /// <param name="ray">The ray we are working with.</param>
    /// <param name="t">The intersection distance along our curve.</param>
    /// <returns>The intersection information or <c>null</c>, if the translation of the
    /// intersection point to 3D ends up beyond our min/max Y bounds.</returns>
    private SimpleIntersection IntersectionDataAt(Ray ray, double t)
    {
        double t3d = GetRayDistance(ray, _curve.GetPoint(t));

        return double.IsNaN(t3d) ? null : new SimpleIntersection(t3d, _curve.NormalAt(t));
    }
}
