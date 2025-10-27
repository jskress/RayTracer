using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This class provides a representation of a quadratic Bézier curve, providing the common
/// math we need.
/// </summary>
internal class QuadCurve : IPathSegment
{
    private static readonly double[] Empty = [];

    /// <summary>
    /// This property holds the control point of the curve.
    /// </summary>
    internal TwoDPoint Control { get; private set; }

    private TwoDPoint _start;
    private TwoDPoint _end;
    private double _aX;
    private double _aY;
    private double _bX;
    private double _bY;

    internal QuadCurve(TwoDPoint start, TwoDPoint control, TwoDPoint end)
    {
        SetPoints(start, control, end);
    }

    /// <summary>
    /// This method is used to set up our coefficients based on the given control points.
    /// </summary>
    /// <param name="start">The point at which the curve starts.</param>
    /// <param name="control">The first control point for the curve.</param>
    /// <param name="end">The point at which the curve ends.</param>
    private void SetPoints(TwoDPoint start, TwoDPoint control, TwoDPoint end)
    {
        _start = start;
        Control = control;
        _end = end;

        _aX = _start.X - 2 * control.X + end.X;
        _aY = _start.Y - 2 * control.Y + end.Y;
        _bX = _start.X - control.X;
        _bY = _start.Y - control.Y;
    }

    /// <summary>
    /// This method is used to reverse the direction of this path segment.
    /// </summary>
    public void Reverse()
    {
        SetPoints(_end, Control, _start);
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
        double a;
        double b;
        double c;

        if (lineA.X.Near(lineB.X))
        {
            a = _aX;
            b = -2 * _bX;
            c = _start.X - lineA.X;
        }
        else if (lineA.Y.Near(lineB.Y))
        {
            a = _aY;
            b = -2 * _bY;
            c = _start.Y - lineA.Y;
        }
        else
        {
            double k = (lineA.Y - lineB.Y) / (lineB.X - lineA.X);

            a = k * _aX + _aY;
            b = -2 * (k * _bX + _bY);
            c = k * (_start.X - lineA.X) + _start.Y - lineA.Y;
        }

        return Evaluate(a, b, c)
            .Select(t => new TwoDIntersection
            {
                Distance = t,
                Point = GetPoint(t),
                TwoDNormal = NormalAt(t)
            });
    }

    /// <summary>
    /// This method is used to determine the point for a given distance along our curve.
    /// It is assumed that <c>t</c> is in the [0, 1] interval.
    /// </summary>
    /// <param name="t">The distance along the curve to get the point for.</param>
    /// <returns>The point at the given distance.</returns>
    private TwoDPoint GetPoint(double t)
    {
        double iT = 1 - t;
        double iT2 = iT * iT;
        double iTt2 = 2 * iT * t;
        double t2 = t * t;
        double x = iT2 * _start.X + iTt2 * Control.X + t2 * _end.X;
        double y = iT2 * _start.Y + iTt2 * Control.Y + t2 * _end.Y;

        return new TwoDPoint(x, y);
    }

    /// <summary>
    /// This method is used to determine the normal vector for the given distance along
    /// the curve.
    /// </summary>
    /// <param name="t">The distance along the curve where we want the normal.</param>
    /// <returns>The normal at the given distance.</returns>
    private TwoDVector NormalAt(double t)
    {
        TwoDPoint derivative = GetDerivative(t);
        double q = Math.Sqrt(derivative.X * derivative.X + derivative.Y * derivative.Y);
        // Invert so the normal points the right way.
        double dx = derivative.Y / q;
        double dy = -derivative.X / q;

        return new TwoDVector(dx, dy);
    }

    /// <summary>
    /// This is a helper method to calculate the derivative at the indicated point on our
    /// curve.
    /// </summary>
    /// <param name="t">The time value that determines the point on our curve.</param>
    /// <returns>The derivative at that point.</returns>
    private TwoDPoint GetDerivative(double t)
    {
        double d1X = 2 * (Control.X - _start.X);
        double d1Y = 2 * (Control.Y - _start.Y);
        double d2X = 2 * (_end.X - Control.X);
        double d2Y = 2 * (_end.Y - Control.Y);
        double x = (1 - t) * d1X + t * d2X;
        double y = (1 - t) * d1Y + t * d2Y;

        return new TwoDPoint(x, y);
    }

    /// <summary>
    /// This method is used to evaluate the given coefficients of a quadratic equation.
    /// </summary>
    /// <param name="a">The <c>a</c> coefficient to evaluate.</param>
    /// <param name="b">The <c>a</c> coefficient to evaluate.</param>
    /// <param name="c">The <c>c</c> coefficient to evaluate.</param>
    /// <returns>The array of solutions to the given coefficients.</returns>
    private static double[] Evaluate(double a, double b, double c)
    {
        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            return Empty;

        double t0;
        double t1 = double.NaN;

        a *= 2;
        b = -b;

        if (discriminant == 0)
            t0 = Clip(b / a);
        else
        {
            discriminant = Math.Sqrt(discriminant);

            t0 = Clip((b - discriminant) / a);
            t1 = Clip((b + discriminant) / a);
            
            if (!double.IsNaN(t0) && !double.IsNaN(t1) && t0.Near(t1))
                t1 = double.NaN;
            
            if (double.IsNaN(t0) && !double.IsNaN(t1))
                (t0, t1) = (t1, t0);
        }

        return double.IsNaN(t0) ? Empty : double.IsNaN(t1) ? [t0] : [t0, t1];
    }

    /// <summary>
    /// This method is used to clip the given value to the [0, 1] interval.  If the value
    ///  is outside the interval, then <c>NaN</c> is returned.
    /// </summary>
    /// <param name="value">The value to clip.</param>
    /// <returns>The clipped value.</returns>
    private static double Clip(double value)
    {
        return value is < 0 or > 1 ? double.NaN : value;
    }
}
