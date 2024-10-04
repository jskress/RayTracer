using RayTracer.Basics;
using RayTracer.Extensions;

namespace RayTracer.Graphics;

/// <summary>
/// This class provides a representation of a quadratic Bézier curve, providing the common
/// math we need.
/// </summary>
internal class QuadCurve
{
    private static readonly double[] Empty = [];

    private readonly TwoDPoint _start;
    private readonly TwoDPoint _control;
    private readonly TwoDPoint _end;

    internal QuadCurve(TwoDPoint start, TwoDPoint control, TwoDPoint end)
    {
        _start = start;
        _control = control;
        _end = end;
    }

    /// <summary>
    /// This method is used to determine the point for a given distance along our curve.
    /// It is assumed that <c>t</c> is in the [0, 1] interval.
    /// </summary>
    /// <param name="t">The distance along the curve to get the point for.</param>
    /// <returns>The point at the given distance.</returns>
    internal TwoDPoint GetPoint(double t)
    {
        double iT = 1 - t;
        double iT2 = iT * iT;
        double iTt2 = 2 * iT * t;
        double t2 = t * t;
        double x = iT2 * _start.X + iTt2 * _control.X + t2 * _end.X;
        double y = iT2 * _start.Y + iTt2 * _control.Y + t2 * _end.Y;

        return new TwoDPoint(x, y);
    }

    /// <summary>
    /// This method is used to determine the normal vector for the given distance along
    /// the curve.
    /// </summary>
    /// <param name="t">The distance along the curve where we want the normal.</param>
    /// <returns>The normal at the given distance.</returns>
    internal Vector NormalAt(double t)
    {
        TwoDPoint derivative = GetDerivative(t);
        double q = Math.Sqrt(derivative.X * derivative.X + derivative.Y * derivative.Y);
        // Invert so the normal points the right way.
        double dx = derivative.Y / q;
        double dy = -derivative.X / q;

        return new Vector(dx, 0, dy);
    }

    /// <summary>
    /// This is a helper method to calculate the derivative at the indicated point on our
    /// curve.
    /// </summary>
    /// <param name="t">The time value that determines the point on our curve.</param>
    /// <returns>The derivative at that point.</returns>
    private TwoDPoint GetDerivative(double t)
    {
        double d1X = 2 * (_control.X - _start.X);
        double d1Y = 2 * (_control.Y - _start.Y);
        double d2X = 2 * (_end.X - _control.X);
        double d2Y = 2 * (_end.Y - _control.Y);
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
    internal static double[] Evaluate(double a, double b, double c)
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
