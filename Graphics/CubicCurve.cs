
namespace RayTracer.Graphics;

/// <summary>
/// This class provides a representation of a quadratic Bézier curve, providing the common
/// math we need.
/// </summary>
public class CubicCurve : IPathSegment
{
    private const double OneThird = 1d / 3d;

    private static readonly double Sqrt3 = Math.Sqrt(3d);

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
    /// This method is used to produce a copy of this path segment, but reversed, and with
    /// points mirrored around the Y axis.
    /// </summary>
    /// <returns>A reversed copy of this segment.</returns>
    public IPathSegment ReversedMirrorCopy()
    {
        return new CubicCurve(
            _end.MirrorAroundX(), _cp2.MirrorAroundX(), _cp1.MirrorAroundX(),
            _start.MirrorAroundX());
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
    /// <param name="coefficients">The coefficients to use in determining the roots.</param>
    /// <returns>The roots of the equation solution.</returns>
    private static double[] CubicRoots(double[] coefficients)
    {
        double a = coefficients[1] / coefficients[0];
        double b = coefficients[2] / coefficients[0];
        double c = coefficients[3] / coefficients[0];
        double aThird = a / 3;
        double q = (3 * b - Math.Pow(a, 2)) / 9;
        double r = (9 * a * b - 27 * c - 2 * Math.Pow(a, 3)) / 54;
        double discriminant = Math.Pow(q, 3) + Math.Pow(r, 2);
        double t0;
        double t1 = -1;
        double t2 = -1;

        if (discriminant >= 0)
        {
            double sqrtDiscriminant = Math.Sqrt(discriminant);
            double plusR = r + sqrtDiscriminant;
            double minusR = r - sqrtDiscriminant;
            double s = Sign(plusR) * Math.Pow(Math.Abs(plusR), OneThird);
            double t = Sign(minusR) * Math.Pow(Math.Abs(minusR), OneThird);
            double negAThird = -aThird;
            double sPlusT = s + t;
            double imaginary = Math.Abs(Sqrt3 * (s - t) / 2);

            t0 = negAThird + sPlusT;

            if (imaginary == 0)
                t1 = negAThird - sPlusT / 2;
        }
        else
        {
            double th = Math.Acos(r / Math.Sqrt(-Math.Pow(q, 3)));
            double twiceSqrtNegQ = 2 * Math.Sqrt(-q);

            t0 = twiceSqrtNegQ * Math.Cos(th / 3) - aThird;
            t1 = twiceSqrtNegQ * Math.Cos((th + 2 * Math.PI) / 3) - aThird;
            t2 = twiceSqrtNegQ * Math.Cos((th + 4 * Math.PI) / 3) - aThird;
        }

        return new [] { t0, t1, t2 }
            .Where(t => t is >= 0 and <= 1)
            .ToArray();
    }

    /// <summary>
    /// This method returns the sign of the given number, either <c>-1</c>, if <c>number</c>
    /// is less than zero, or <c>1</c> if not.
    /// </summary>
    /// <remarks>
    /// We have this because it behaves slightly differently than the equivalent <c>Math</c>
    /// method.
    /// </remarks>
    /// <param name="number">The number to return the sign value for.</param>
    /// <returns><c>-1</c> or <c>1</c>, based on the number.</returns>
    private static double Sign(double number)
    {
        return number < 0 ? -1 : 1;
    }
}
