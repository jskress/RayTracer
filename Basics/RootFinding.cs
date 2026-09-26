using Complex = System.Numerics.Complex;
using MathNet.Numerics;
using RayTracer.Extensions;

namespace RayTracer.Basics;

/// <summary>
/// This class finds the real roots of the polynomials that ray intersections come down to,
/// preferring the exact solver but never depending on it.
/// <para>
/// The exact solver finds a polynomial's roots as the eigenvalues of its companion matrix, and
/// that iteration is not guaranteed to converge.  What stalls it is four roots of all but one
/// size -- two conjugate pairs agreeing to a dozen digits, or two +/- pairs, each nearly doubled
/// -- and that is not a contrived shape.  A blob's sextic makes it; so does a torus's quartic, for
/// a ray from the torus's centre that grazes its tube, and a tube's resultant, for a ray square
/// across a symmetric arch that grazes both legs at once.  Every failure measured has had four
/// roots of one size, which a cubic cannot have, and two million cubic solves never failed once.
/// When it happens the solver throws, and an exception thrown here is thrown out of the scanner
/// and takes the whole picture down with it.
/// </para>
/// <para>
/// So a failure falls back to walking an interval for sign changes and bisecting each one.  That
/// is slower and it is not exact, but it *always terminates*.  What it cannot see is a root the
/// polynomial merely touches without crossing, or a pair closer together than one step of the
/// walk; both are a grazing sliver of surface, and losing one on the rare ray where the exact
/// solver has already given up is a far better answer than abandoning the picture.
/// </para>
/// </summary>
public static class RootFinding
{
    /// <summary>
    /// This method returns the real roots of a polynomial.  Where the exact solver succeeds, its
    /// real roots are returned wherever they lie, just as they always were; the interval is only
    /// where the fallback looks, so it must hold every root the caller means to keep.
    /// </summary>
    /// <param name="polynomial">The polynomial to solve.</param>
    /// <param name="intervalStart">The start of the interval the fallback walks.</param>
    /// <param name="intervalEnd">The end of the interval the fallback walks.</param>
    /// <returns>The real roots that were found.</returns>
    public static IEnumerable<double> RealRootsOf(
        Polynomial polynomial, double intervalStart, double intervalEnd)
    {
        Complex[] roots;

        try
        {
            roots = polynomial.Roots();
        }
        catch (Exception)
        {
            return ByBisection(polynomial.Coefficients, intervalStart, intervalEnd);
        }

        return roots
            .Where(root => root.Imaginary.Near(0))
            .Select(root => root.Real);
    }

    /// <summary>
    /// This method finds roots the way that cannot fail: it walks the interval, and wherever the
    /// polynomial changes sign between one step and the next it has a root bracketed, which it
    /// then closes in on by halving.
    /// </summary>
    /// <param name="coefficients">The polynomial's coefficients, in ascending order.</param>
    /// <param name="intervalStart">The start of the sub-interval to walk.</param>
    /// <param name="intervalEnd">The end of the sub-interval to walk.</param>
    /// <returns>The roots that were bracketed and closed in on.</returns>
    public static IEnumerable<double> ByBisection(
        double[] coefficients, double intervalStart, double intervalEnd)
    {
        // Fine enough to separate the roots of a sextic across one blob primitive's span, and the
        // cost does not signify when this runs on a ray in a hundred thousand.
        const int Steps = 128;

        // An interval may be unbounded: a blob's plane component whose slab a ray runs along the
        // length of is switched on for the whole of that ray and never off again.  A walk needs
        // somewhere to start and stop, and Cauchy's bound gives one that costs nothing -- every
        // root of a polynomial lies within one plus the largest of the other coefficients over the
        // leading one, so clamping to it cannot pass over a root.
        if (double.IsInfinity(intervalStart) || double.IsInfinity(intervalEnd))
        {
            double bound = BoundOnRootsOf(coefficients);

            intervalStart = Math.Max(intervalStart, -bound);
            intervalEnd = Math.Min(intervalEnd, bound);
        }

        double step = (intervalEnd - intervalStart) / Steps;
        double lowT = intervalStart;
        double low = ValueAt(coefficients, lowT);

        for (int index = 1; index <= Steps; index++)
        {
            double highT = index == Steps ? intervalEnd : intervalStart + step * index;
            double high = ValueAt(coefficients, highT);

            if (low == 0)
                yield return lowT;
            else if (low < 0 != high < 0)
                yield return Bisect(coefficients, lowT, highT, low);

            lowT = highT;
            low = high;
        }

        if (low == 0)
            yield return lowT;
    }

    /// <summary>
    /// This method closes in on a root already known to lie between two points, by repeatedly
    /// halving the gap and keeping whichever half still straddles it.  Fifty-odd halvings take a
    /// bracket down to the last bit a double has, so the loop is capped there rather than trusted
    /// to a tolerance.
    /// </summary>
    /// <param name="coefficients">The polynomial's coefficients, in ascending order.</param>
    /// <param name="lowT">One side of the bracket.</param>
    /// <param name="highT">The other side of the bracket.</param>
    /// <param name="low">The polynomial's value at <paramref name="lowT"/>.</param>
    /// <returns>The root.</returns>
    private static double Bisect(double[] coefficients, double lowT, double highT, double low)
    {
        const int Halvings = 60;

        for (int index = 0; index < Halvings; index++)
        {
            double middleT = (lowT + highT) / 2;

            if (middleT <= lowT || middleT >= highT)
                break;

            double middle = ValueAt(coefficients, middleT);

            if (middle == 0)
                return middleT;

            if (low < 0 != middle < 0)
                highT = middleT;
            else
                (lowT, low) = (middleT, middle);
        }

        return (lowT + highT) / 2;
    }

    /// <summary>
    /// This method returns the size of the largest coefficient, which is what the others are
    /// judged against wherever one of them has to be told apart from nothing.
    /// </summary>
    /// <param name="coefficients">The polynomial's coefficients, in ascending order.</param>
    /// <returns>The largest coefficient by magnitude.</returns>
    public static double LargestOf(double[] coefficients)
    {
        double largest = 0;

        foreach (double coefficient in coefficients)
            largest = Math.Max(largest, Math.Abs(coefficient));

        return largest;
    }

    /// <summary>
    /// This method returns a radius that every real root of the polynomial is known to lie within,
    /// by Cauchy's bound: one plus the largest of the trailing coefficients divided by the leading
    /// one.  A leading coefficient of nought would make that meaningless, so the highest one that
    /// is actually there is the one used.
    /// </summary>
    /// <param name="coefficients">The polynomial's coefficients, in ascending order.</param>
    /// <returns>A radius containing every root.</returns>
    private static double BoundOnRootsOf(double[] coefficients)
    {
        double largestCoefficient = LargestOf(coefficients);
        int leading = coefficients.Length - 1;

        // Whether the leading coefficient is big enough to divide by is a question about the sizes
        // of the coefficients beside it, not about a fixed number.
        while (leading > 0 && coefficients[leading].IsNegligibleBeside(largestCoefficient))
            leading--;

        if (leading == 0)
            return 1;

        double largest = 0;

        for (int index = 0; index < leading; index++)
            largest = Math.Max(largest, Math.Abs(coefficients[index]));

        return 1 + largest / Math.Abs(coefficients[leading]);
    }

    /// <summary>
    /// This method evaluates the polynomial at a point, by Horner's method.
    /// </summary>
    /// <param name="coefficients">The polynomial's coefficients, in ascending order.</param>
    /// <param name="t">The point to evaluate it at.</param>
    /// <returns>The polynomial's value there.</returns>
    private static double ValueAt(double[] coefficients, double t)
    {
        double value = 0;

        for (int index = coefficients.Length - 1; index >= 0; index--)
            value = value * t + coefficients[index];

        return value;
    }
}
