using System.Numerics;
using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class provides the work of solving 2nd, 3rd and 4th order equations.
/// </summary>
public static class Polynomials
{
    // The solvers here are from POVRay.  They work the best that I found.

    // A coefficient smaller than SmallEnough is considered to be zero (0.0).
    private const double SmallEnough = 1.0e-10;
    private const double TwoMPi3  = 2.0943951023931954923084;
    private const double FourMPi3 = 4.1887902047863909846168;

    /// <summary>
    /// This method may be called to solve an n-order polynomial.
    /// </summary>
    /// <returns></returns>
    public static double[] Solve(double[] coefficients, double epsilon = DoubleExtensions.Epsilon)
    {
        int order = coefficients.Length - 1;
        int i = 0;

        while (coefficients[i].Near(0, SmallEnough) && i < order)
            i++;

        coefficients = coefficients[i..];
        order -= i;

        bool downgrade = order > 2 && epsilon > 0 &&
                         coefficients[order - 1] != 0.0 &&
                         Math.Abs(coefficients[order] / coefficients[order - 1]) < epsilon;

        switch (order)
        {
            case 1:
                if (coefficients[0] != 0)
                    return [-coefficients[1] / coefficients[0]];
                break;
            case 2:
                return SolveQuadraticPolynomial(coefficients);
            case 3:
                return downgrade
                    ? SolveQuadraticPolynomial(coefficients)
                    : SolveCubicPolynomial(coefficients);
            case 4:
                return downgrade
                    ? SolveCubicPolynomial(coefficients)
                    : SolveQuarticPolynomial(coefficients);
            default:
                throw new Exception("Internal error: Can't solve polynomials of an order higher than 4.");
                // return downgrade
                //     ? SolvePoly(coefficients[..^1])
                //     : SolvePoly(coefficients);
        }

        return null;
    }

    /// <summary>
    /// This method is used to produce the list of real solutions to a quartic polynomial.
    /// If no solutions exist, then <c>null</c> will be returned.
    /// </summary>
    /// <returns>The list of solutions or <c>null</c>.</returns>
    private static double[] SolveQuarticPolynomial(double[] coefficients)
    {
        double c0 = coefficients[0];
        double c1, c2, c3, c4;
        double z, d2;

        if (c0.Near(1))
        {
            c1 = coefficients[1];
            c2 = coefficients[2];
            c3 = coefficients[3];
            c4 = coefficients[4];
        }
        else
        {
            c1 = coefficients[1] / c0;
            c2 = coefficients[2] / c0;
            c3 = coefficients[3] / c0;
            c4 = coefficients[4] / c0;
        }

        /* Compute the cubic resolvent */
        double c1Squared = c1 * c1;
        double p = -0.375 * c1Squared + c2;
        double q = 0.125 * c1Squared * c1 - 0.5 * c1 * c2 + c3;
        double r = -0.01171875 * c1Squared * c1Squared + 0.0625 * c1Squared * c2 - 0.25 * c1 * c3 + c4;
        double[] cubic = [
            1.0,
            -0.5 * p,
            -r,
            0.5 * r * p - 0.125 * q * q
        ];
        double[] roots = SolveCubicPolynomial(cubic);

        if (roots.Length > 0)
            z = roots[0];
        else
            return null;
        
        double d1 = 2.0 * z - p;

        if (d1 < 0.0)
        {
            if (d1 > -SmallEnough)
                d1 = 0.0;
            else
                return null;
        }

        if (d1 < SmallEnough)
        {
            d2 = z * z - r;

            if (d2 < 0.0)
                return null;

            d2 = Math.Sqrt(d2);
        }
        else
        {
            d1 = Math.Sqrt(d1);
            d2 = 0.5 * q / d1;
        }

        /* Set up useful values for the quadratic factors */
        double q1 = d1 * d1;
        double q2 = -0.25 * c1;

        List<double> result = [];

        /* Solve the first quadratic */

        p = q1 - 4.0 * (z - d2);

        switch (p)
        {
            case 0:
                result.Add(-0.5 * d1 - q2);
                break;
            case > 0:
                p = Math.Sqrt(p);
                result.Add(-0.5 * (d1 + p) + q2);
                result.Add(-0.5 * (d1 - p) + q2);
                break;
        }

        /* Solve the second quadratic */

        p = q1 - 4.0 * (z + d2);

        switch (p)
        {
            case 0:
                result.Add(0.5 * d1 - q2);
                break;
            case > 0:
                p = Math.Sqrt(p);
                result.Add(0.5 * (d1 + p) + q2);
                result.Add(0.5 * (d1 - p) + q2);
                break;
        }

        return result.ToOrderedArray();
    }

    /// <summary>
    /// This method is used to produce the list of real solutions to a cubic polynomial.
    /// If no solutions exist, then <c>null</c> will be returned.
    /// </summary>
    /// <returns>The list of solutions or <c>null</c>.</returns>
    private static double[] SolveCubicPolynomial(double[] coefficients)
    {
        double sQ;
        double a1, a2, a3;
        double a0 = coefficients[0];

        if (a0 == 0.0)
            return SolveQuadraticPolynomial(coefficients[1..]);

        if (a0.Near(1))
        {
            a1 = coefficients[1];
            a2 = coefficients[2];
            a3 = coefficients[3];
        }
        else
        {
            a1 = coefficients[1] / a0;
            a2 = coefficients[2] / a0;
            a3 = coefficients[3] / a0;
        }

        double aSquared = a1 * a1;
        double q = (aSquared - 3.0 * a2) / 9.0;
        double r = (a1 * (aSquared - 4.5 * a2) + 13.5 * a3) / 27.0;
        double qCubed = q * q * q;
        double rSquared = r * r;
        double d = qCubed - rSquared;
        double an = a1 / 3.0;
        List<double> result = [];

        if (d >= 0.0)
        {
            /* Three real roots. */
            d = r / Math.Sqrt(qCubed);

            double theta = Math.Acos(d) / 3.0;

            sQ = -2.0 * Math.Sqrt(q);

            result.Add(sQ * Math.Cos(theta) - an);
            result.Add(sQ * Math.Cos(theta + TwoMPi3) - an);
            result.Add(sQ * Math.Cos(theta + FourMPi3) - an);
        }
        else
        {
            sQ = Math.Pow(Math.Sqrt(rSquared - qCubed) + Math.Abs(r), 1.0 / 3.0);

            if (r < 0)
                result.Add(sQ + q / sQ - an);
            else
                result.Add(-(sQ + q / sQ) - an);
        }

        return result.ToOrderedArray();
    }

    /// <summary>
    /// This method is used to produce the list of real solutions to a quadratic polynomial.
    /// If no solutions exist, then <c>null</c> will be returned.
    /// </summary>
    /// <returns>The list of solutions or <c>null</c>.</returns>
    private static double[] SolveQuadraticPolynomial(double[] coefficients)
    {
        double a = coefficients[0];
        double b = -coefficients[1];
        double c = coefficients[2];

        if (a == 0.0)
            return b == 0.0 ? null : [c / b];

        // normalize the coefficients
        b /= a;
        c /= a;
        a = 1.0;

        double d = b * b - 4.0 * a * c;

        /* Treat values of d around 0 as 0. */
        switch (d)
        {
            case > -SmallEnough and < SmallEnough:
                return [0.5 * b / a];
            case < 0.0:
                return null;
        }

        d = Math.Sqrt(d);

        double t = 2.0 * a;

        return new List<double>
            {
                (b + d) / t,
                (b - d) / t
            }
            .ToOrderedArray();
    }
}

internal static class DoubleListExtensions
{
    internal static void AddIfReal(this List<double> list, Complex number)
    {
        if (number.Imaginary.Near(0))
            list.Add(number.Real);
    }

    internal static double[] ToOrderedArray(this List<double> list)
    {
        return list.Count == 0 ? null : list
            .Where(number => !double.IsNaN(number))
            .OrderBy(number => number)
            .ToArray();
    }
}
