using MathNet.Numerics;
using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This class provides a representation of a quadratic Bézier curve, providing the common
/// math we need.
/// </summary>
public class CubicCurve : IPathSegment
{
    /// <summary>
    /// This property exposes the points that define this segment.
    /// </summary>
    public TwoDPoint[] Points => [_start, _cp1, _cp2, _end];

    private TwoDPoint _start;
    private TwoDPoint _cp1;
    private TwoDPoint _cp2;
    private TwoDPoint _end;
    private double[] _xCoefficients;
    private double[] _yCoefficients;

    internal CubicCurve(TwoDPoint start, TwoDPoint cp1, TwoDPoint cp2, TwoDPoint end)
    {
        SetPoints(start, cp1, cp2, end);
    }

    /// <summary>
    /// This method is used to set up our coefficients based on the given control points.
    /// </summary>
    /// <param name="start">The point at which the curve starts.</param>
    /// <param name="cp1">The first control point for the curve.</param>
    /// <param name="cp2">The second control point for the curve.</param>
    /// <param name="end">The point at which the curve ends.</param>
    private void SetPoints(TwoDPoint start, TwoDPoint cp1, TwoDPoint cp2, TwoDPoint end)
    {
        _start = start;
        _cp1 = cp1;
        _cp2 = cp2;
        _end = end;
        _xCoefficients = [
            -start.X + 3 * cp1.X + -3 * cp2.X + end.X,
            3 * start.X - 6 * cp1.X + 3 * cp2.X,
            -3 * start.X + 3 * cp1.X,
            start.X
        ];
        _yCoefficients = [
            -start.Y + 3 * cp1.Y + -3 * cp2.Y + end.Y,
            3 * start.Y - 6 * cp1.Y + 3 * cp2.Y,
            -3 * start.Y + 3 * cp1.Y,
            start.Y
        ];
    }

    /// <summary>
    /// This method is used to reverse the direction of this path segment.
    /// </summary>
    public void Reverse()
    {
        SetPoints(_end, _cp2, _cp1, _start);
    }

    /// <summary>
    /// This method returns the point on this curve at the given parameter.
    /// </summary>
    /// <param name="t">The parameter to evaluate at, from 0 (the curve's start) to 1 (its
    /// end).</param>
    /// <returns>The point at that parameter.</returns>
    public TwoDPoint GetPoint(double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;
        double x = _xCoefficients[0] * t3 + _xCoefficients[1] * t2 + _xCoefficients[2] * t + _xCoefficients[3];
        double y = _yCoefficients[0] * t3 + _yCoefficients[1] * t2 + _yCoefficients[2] * t + _yCoefficients[3];

        return new TwoDPoint(x, y);
    }

    /// <summary>
    /// This method is used to locate the intersection points, if any, where the given ray
    /// intersects this curve.
    /// </summary>
    /// <param name="ray">The ray to test.</param>
    /// <returns>An array of the intersection data.
    /// If the ray doesn't intersect the curve, the enumerable must be empty.</returns>
    public IEnumerable<TwoDIntersection> GetIntersections(TwoDRay ray)
    {
        TwoDPoint point = ray.Origin + ray.Direction;
        TwoDPoint lineA = new TwoDPoint(ray.Origin.X, ray.Origin.Y);
        TwoDPoint lineB = new TwoDPoint(point.X, point.Y);
        double a = lineB.Y - lineA.Y;
        double b = lineA.X - lineB.X;
        double[] roots = GetRoots(lineA, lineB);

        return roots
            .Select(t =>
            {
                double t2 = t * t;
                double t3 = t2 * t;
                double x = _xCoefficients[0] * t3 + _xCoefficients[1] * t2 + _xCoefficients[2] * t + _xCoefficients[3];
                double y = _yCoefficients[0] * t3 + _yCoefficients[1] * t2 + _yCoefficients[2] * t + _yCoefficients[3];
                double s = a == 0 && b == 0
                    ? 0
                    : a != 0
                        ? (y - lineA.Y) / a
                        : (x - lineA.X) / -b;

                return s >= 0
                    ? new TwoDIntersection
                    {
                        Distance = t,
                        Point = new TwoDPoint(x, y),
                        TwoDNormal = NormalAt(t)
                    }
                    : null;
            })
            .Where(item => item != null);
    }

    /// <summary>
    /// This method counts how many times this curve crosses the horizontal line through the given
    /// point, strictly to its right.  See <see cref="IPathSegment.CountCrossingsToTheRight"/> for
    /// what the count is for and why the straddle test comes first.
    /// <para>
    /// The line handed to <see cref="GetRoots"/> is the one <see cref="GetIntersections"/> would
    /// build for a rightward horizontal ray, so the roots are the same.  For that line
    /// <c>GetIntersections</c>' own forward test reduces to "the crossing is at or right of the
    /// point", which the strict comparison here subsumes, so no crossing is gained or lost by
    /// leaving it out.  Only each root's X is worked out, rather than an intersection assembled
    /// around it.
    /// </para>
    /// <para>
    /// Unlike the other segments this one still allocates, because the roots come from
    /// <c>MathNet</c>'s polynomial solver, which builds its own arrays.  Getting rid of that would
    /// mean replacing the solver, which would change the roots it finds; the wrappers around it
    /// are what is dropped here.
    /// </para>
    /// </summary>
    /// <param name="point">The point whose horizontal line is to be crossed.</param>
    /// <returns>The number of crossings strictly to the right of the point.</returns>
    public int CountCrossingsToTheRight(TwoDPoint point)
    {
        bool startAbove = _start.Y > point.Y;

        // The curve stays within the hull of its defining points, so if they all sit on one side
        // of the test line, it cannot reach it.
        if (startAbove == _cp1.Y > point.Y &&
            startAbove == _cp2.Y > point.Y &&
            startAbove == _end.Y > point.Y)
            return 0;

        int crossings = 0;

        foreach (double t in GetRoots(point, new TwoDPoint(point.X + 1, point.Y)))
        {
            double t2 = t * t;
            double t3 = t2 * t;
            double x = _xCoefficients[0] * t3 + _xCoefficients[1] * t2 +
                       _xCoefficients[2] * t + _xCoefficients[3];

            if (x > point.X)
                crossings++;
        }

        return crossings;
    }

    /// <summary>
    /// This method returns the intersection information of our curve with the given line.
    /// Each entry returned contains both the intersection distance along the curve, in
    /// the [0, 1] interval, and the point of intersection.
    /// </summary>
    /// <param name="lineA">The first endpoint of the line to check.</param>
    /// <param name="lineB">The second endpoint of the line to check.</param>
    /// <returns>The array of intersection information tuples.</returns>
    internal (double, TwoDPoint)[] GetIntersectionData(TwoDPoint lineA, TwoDPoint lineB)
    {
        double a = lineB.Y - lineA.Y;
        double b = lineA.X - lineB.X;
        double[] roots = GetRoots(lineA, lineB);

        return roots
            .Select(t =>
            {
                double t2 = t * t;
                double t3 = t2 * t;
                double x = _xCoefficients[0] * t3 + _xCoefficients[1] * t2 + _xCoefficients[2] * t + _xCoefficients[3];
                double y = _yCoefficients[0] * t3 + _yCoefficients[1] * t2 + _yCoefficients[2] * t + _yCoefficients[3];
                double s = a == 0 && b == 0
                    ? 0
                    : a != 0
                        ? (y - lineA.Y) / a
                        : (x - lineA.X) / -b;

                return s >= 0 ? (t, new TwoDPoint(x, y)) : default;
            })
            .Where(item => item != default)
            .ToArray();
    }

    /// <summary>
    /// This method returns the intersection information of our curve with the given line.
    /// Each entry returned contains both the intersection distance along the curve, in
    /// the [0, 1] interval, and the point of intersection.
    /// </summary>
    /// <param name="lineA">The first endpoint of the line to check.</param>
    /// <param name="lineB">The second endpoint of the line to check.</param>
    /// <returns>The array of intersection information tuples.</returns>
    private double[] GetRoots(TwoDPoint lineA, TwoDPoint lineB)
    {
        double a = lineB.Y - lineA.Y;
        double b = lineA.X - lineB.X;
        double c = lineA.X * -a + lineA.Y * -b;
        double[] coefficients = [
            a * _xCoefficients[0] + b * _yCoefficients[0],
            a * _xCoefficients[1] + b * _yCoefficients[1],
            a * _xCoefficients[2] + b * _yCoefficients[2],
            a * _xCoefficients[3] + b * _yCoefficients[3] + c
        ];

        return CubicRoots(coefficients);
    }

    /// <summary>
    /// This method is used to determine the normal vector for the given distance along
    /// the curve.
    /// </summary>
    /// <param name="t">The distance along the curve where we want the normal.</param>
    /// <returns>The normal at the given distance.</returns>
    private TwoDVector NormalAt(double t)
    {
        double t2 = t * t;
        // This gives us our tangent line...
        double x = 3 * _xCoefficients[0] * t2 + 2 * _xCoefficients[1] * t + _xCoefficients[2];
        double y = 3 * _yCoefficients[0] * t2 + 2 * _yCoefficients[1] * t + _yCoefficients[2];

        // And this makes a normal out of it.
        return new TwoDVector(y, -x);
    }

    /// <summary>
    /// This method solves the cubic equation for the given set of coefficients.  The
    /// roots returned are guaranteed to be in the [0, 1] interval.
    /// </summary>
    /// <param name="coefficients">The coefficients to use in determining the roots, given
    /// in descending order (the t^3 term first).</param>
    /// <returns>The roots of the equation solution.</returns>
    private static double[] CubicRoots(double[] coefficients)
    {
        // Trim leading (highest-degree) coefficients that are negligible relative to the
        // polynomial's overall scale.  Building these coefficients above involves
        // subtracting near-equal quantities (e.g. -P0+3P1-3P2+P3), which for a curve whose
        // Y coordinate happens to vary linearly or quadratically in t (an entirely ordinary
        // curve shape, not a contrived one) leaves a tiny floating-point residual instead of
        // an exact zero.  A general cubic root-finder doesn't know that residual is really
        // zero, so it solves the wrong (numerically near-singular) cubic and returns
        // garbage roots instead of recognizing the true, lower-degree equation.
        double maxMagnitude = coefficients.Select(Math.Abs).Max();
        int start = 0;

        while (start < coefficients.Length - 1 && coefficients[start].Near(0, maxMagnitude * 1e-9))
            start++;

        // MathNet.Numerics.Polynomial expects coefficients in ascending order (the
        // constant term first), the opposite of how we build them above.
        Polynomial polynomial = new (coefficients[start..].Reverse().ToArray());

        return polynomial.Roots()
            .Where(root => root.Imaginary.Near(0))
            .Select(root => root.Real)
            .Where(t => t is >= 0 and <= 1)
            .ToArray();
    }
}
