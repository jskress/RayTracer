using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using RayTracer.Basics;
using RayTracer.Extensions;
using Complex = System.Numerics.Complex;

namespace RayTracer.Geometry;

/// <summary>
/// This class holds the numeric machinery shared by <see cref="TubeQuadSegment"/> and
/// <see cref="TubeCubicSegment"/>: both eliminate the curve parameter <c>u</c> between the
/// envelope condition and the sphere-family equation by evaluating their resultant (via a
/// Sylvester matrix determinant) at enough sample points to reconstruct its coefficients
/// exactly, then solve the result for the ray's distance parameter <c>t</c> -- the only
/// thing that differs between them is the degree of the curve (and so the size of
/// everything involved).  All polynomials here use ascending coefficient arrays (index is
/// the power), matching <see cref="MathNet.Numerics.Polynomial"/>'s own convention.
/// </summary>
internal static class TubeCurveMath
{
    /// <summary>
    /// This method finds every point where a ray is exactly tangent to one of the
    /// interpolated spheres making up a curved segment's envelope surface -- the general
    /// "solve the resultant, then recover u" algorithm shared by every curve degree.
    /// </summary>
    /// <param name="g">The family equation, as a polynomial in u whose coefficients are
    /// themselves (up to quadratic) polynomials in t.</param>
    /// <param name="sampleCount">The number of sample points to use when reconstructing the
    /// resultant -- one more than its known degree in t.</param>
    /// <param name="tMin">The low end of the ray-distance range of interest.</param>
    /// <param name="tMax">The high end of the ray-distance range of interest.</param>
    /// <returns>Each valid (T, U) hit found, with U strictly between 0 and 1.</returns>
    public static IEnumerable<(double T, double U)> FindEnvelopeHits(
        double[][] g, int sampleCount, double tMin, double tMax)
    {
        // Sample in a normalized s in [-1, 1] domain, rather than raw ray-distance t
        // values, and only map back to t afterward.  A high-degree Vandermonde fit is
        // catastrophically ill-conditioned once its nodes are both far from zero and
        // tightly clustered (exactly what happens for a distant or near-grazing ray, where
        // [tMin, tMax] might be, say, [900, 900.07]) -- normalizing first keeps the fit
        // well-conditioned regardless of how far away or how narrow the hit interval is.
        double tMid = (tMin + tMax) / 2;
        double tHalf = (tMax - tMin) / 2;
        double[] sampleSs = new double[sampleCount];
        double[] resultantSamples = new double[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            sampleSs[i] = -1 + 2.0 * i / (sampleCount - 1);

            double sampleT = tMid + sampleSs[i] * tHalf;
            double[] gAtT = EvaluateUPolynomialAt(g, sampleT);
            double[] dgduAtT = Differentiate(gAtT);

            resultantSamples[i] = SylvesterResultant(gAtT, dgduAtT);
        }

        double[] resultantCoefficients = SolveVandermonde(sampleSs, resultantSamples);

        foreach (double s in RealRoots(resultantCoefficients))
        {
            if (s < -1 || s > 1)
                continue;

            double t = tMid + s * tHalf;
            double[] gAtRoot = EvaluateUPolynomialAt(g, t);
            double[] dgduAtRoot = Differentiate(gAtRoot);
            double? u = FindCommonRoot(dgduAtRoot, gAtRoot);

            if (u is > 0 and < 1)
                yield return (t, u.Value);
        }
    }

    /// <summary>
    /// This method determines whether a point already known to be on one of the family's
    /// end spheres (so <paramref name="f"/> is guaranteed a root at u = 0 or u = 1) is
    /// actually on the union's outer boundary, or whether it's swallowed by some
    /// intermediate sphere along the segment.  That reduces to whether <paramref name="f"/>
    /// stays non-negative across the open interval (0, 1): if it has no other roots there,
    /// its sign is constant and a single interior sample settles it; if it does, the point
    /// is swallowed somewhere along the way.
    /// </summary>
    /// <param name="f">The polynomial-in-u (ascending coefficients) for the point being
    /// tested.</param>
    /// <returns><c>true</c>, if the point is on the outer boundary.</returns>
    public static bool IsOnOuterBoundary(double[] f)
    {
        bool hasInteriorRoot = RealRoots(f).Any(u => u is > DoubleExtensions.Epsilon and < 1 - DoubleExtensions.Epsilon);

        return !hasInteriorRoot && Evaluate(f, 0.5) >= 0;
    }

    /// <summary>
    /// This method finds which root of <paramref name="p"/> also (approximately) zeroes
    /// <paramref name="check"/>, which is how the curve parameter is recovered once a valid
    /// ray-distance root has been found: at that distance, the envelope condition and the
    /// sphere-family equation share exactly the one root that matters.  Eliminating a
    /// variable via a resultant can introduce extraneous roots that don't actually solve
    /// the original system, so a candidate is only accepted if it zeroes
    /// <paramref name="check"/> to within a tolerance relative to that polynomial's own
    /// scale -- not just whichever candidate happens to be the least bad.
    /// </summary>
    /// <param name="p">The polynomial (ascending coefficients) to find roots of.</param>
    /// <param name="check">The polynomial (ascending coefficients) each root should also
    /// (approximately) zero.</param>
    /// <returns>The best-matching root, or <c>null</c>, if none of <paramref name="p"/>'s
    /// real roots actually zero <paramref name="check"/>.</returns>
    private static double? FindCommonRoot(double[] p, double[] check)
    {
        double? best = null;
        double bestResidual = double.MaxValue;

        foreach (double root in RealRoots(p))
        {
            double residual = Math.Abs(Evaluate(check, root));

            if (residual < bestResidual)
            {
                bestResidual = residual;
                best = root;
            }
        }

        const double relativeTolerance = 1e-6;
        double checkMagnitude = check.Select(Math.Abs).DefaultIfEmpty(0).Max();
        double tolerance = checkMagnitude * relativeTolerance + DoubleExtensions.Epsilon;

        return best.HasValue && bestResidual <= tolerance ? best : null;
    }

    /// <summary>
    /// This method evaluates a polynomial (ascending coefficients) at the given value.
    /// </summary>
    public static double Evaluate(double[] coefficients, double x)
    {
        double result = 0;
        double power = 1;

        foreach (double coefficient in coefficients)
        {
            result += coefficient * power;
            power *= x;
        }

        return result;
    }

    /// <summary>
    /// This method finds the real roots (ascending coefficients) of a polynomial, using
    /// <see cref="MathNet.Numerics"/>'s general, arbitrary-degree solver.  Trailing
    /// coefficients that are negligible relative to the polynomial's largest one are
    /// trimmed first: our resultant reconstruction always assumes the generic (worst-case)
    /// degree, so a segment whose true degree is lower ends up with high-order coefficients
    /// that are pure numerical noise -- treating them as genuine leading terms would hand
    /// the companion-matrix solver a wildly ill-conditioned problem for no reason.
    /// </summary>
    public static IEnumerable<double> RealRoots(double[] coefficients)
    {
        double[] trimmed = TrimNegligibleTrailingCoefficients(coefficients);

        if (trimmed.Length == 0)
            yield break;

        foreach (Complex root in new Polynomial(trimmed).Roots())
        {
            if (root.Imaginary.Near(0))
                yield return root.Real;
        }
    }

    /// <summary>
    /// This method drops trailing (highest-order) coefficients whose magnitude is
    /// negligible relative to the largest coefficient present, so callers only ever solve
    /// for a polynomial's true degree.
    /// </summary>
    private static double[] TrimNegligibleTrailingCoefficients(double[] coefficients)
    {
        const double relativeTolerance = 1e-9;
        double maxMagnitude = coefficients.Select(Math.Abs).DefaultIfEmpty(0).Max();

        if (maxMagnitude.Near(0))
            return [];

        int end = coefficients.Length;

        while (end > 1 && Math.Abs(coefficients[end - 1]) < maxMagnitude * relativeTolerance)
            end--;

        return coefficients[..end];
    }

    /// <summary>
    /// This method evaluates a "polynomial in u whose coefficients are themselves
    /// polynomials in t" at a specific value of t, producing a plain polynomial in u
    /// (ascending coefficients).
    /// </summary>
    public static double[] EvaluateUPolynomialAt(double[][] uCoefficientsInT, double t)
    {
        double[] result = new double[uCoefficientsInT.Length];

        for (int i = 0; i < uCoefficientsInT.Length; i++)
            result[i] = Evaluate(uCoefficientsInT[i], t);

        return result;
    }

    /// <summary>
    /// This method returns the derivative (ascending coefficients) of a polynomial (also
    /// ascending coefficients).
    /// </summary>
    private static double[] Differentiate(double[] coefficients)
    {
        if (coefficients.Length <= 1)
            return [0];

        double[] result = new double[coefficients.Length - 1];

        for (int i = 1; i < coefficients.Length; i++)
            result[i - 1] = coefficients[i] * i;

        return result;
    }

    /// <summary>
    /// This method computes the resultant of two polynomials (ascending coefficients),
    /// eliminating their shared variable, via the determinant of their Sylvester matrix.
    /// The classical Sylvester construction is only valid when each polynomial's leading
    /// coefficient is its true (nonzero) one, so a degenerate segment whose nominal-degree
    /// leading terms happen to vanish -- e.g. a curved segment whose control points sit
    /// exactly on the line between its ends -- must be trimmed to its actual degree first,
    /// or the matrix comes out singular and every sample reports a bogus zero resultant.
    /// </summary>
    private static double SylvesterResultant(double[] pAscending, double[] qAscending)
    {
        double[] p = TrimLeadingZeros([..pAscending.Reverse()]);
        double[] q = TrimLeadingZeros([..qAscending.Reverse()]);

        if (p.Length == 0 || q.Length == 0)
            return 0;

        int m = p.Length - 1;
        int n = q.Length - 1;
        int size = m + n;

        if (size == 0)
            return 1;

        double[,] matrix = new double[size, size];

        for (int row = 0; row < n; row++)
        for (int i = 0; i < p.Length; i++)
            matrix[row, row + i] = p[i];

        for (int row = 0; row < m; row++)
        for (int i = 0; i < q.Length; i++)
            matrix[n + row, row + i] = q[i];

        return Matrix<double>.Build.DenseOfArray(matrix).Determinant();
    }

    /// <summary>
    /// This method drops leading (near-)zero entries from a descending-order coefficient
    /// array, so the array's length reflects the polynomial's true degree rather than
    /// whatever nominal degree it happened to be built with.
    /// </summary>
    private static double[] TrimLeadingZeros(double[] descending)
    {
        int start = 0;

        while (start < descending.Length && descending[start].Near(0))
            start++;

        return descending[start..];
    }

    /// <summary>
    /// This method reconstructs a polynomial's ascending coefficients from a set of (x, y)
    /// sample pairs, by solving the corresponding Vandermonde system.
    /// </summary>
    private static double[] SolveVandermonde(double[] xs, double[] ys)
    {
        int n = xs.Length;
        double[,] vandermonde = new double[n, n];

        for (int row = 0; row < n; row++)
        {
            double power = 1;

            for (int col = 0; col < n; col++)
            {
                vandermonde[row, col] = power;
                power *= xs[row];
            }
        }

        Matrix<double> matrix = Matrix<double>.Build.DenseOfArray(vandermonde);
        Vector<double> rhs = Vector<double>.Build.DenseOfArray(ys);

        return matrix.Solve(rhs).ToArray();
    }

    /// <summary>
    /// This is a small helper for solving a quadratic equation, handling the degenerate,
    /// linear case where the leading coefficient is (nearly) zero.
    /// </summary>
    public static IEnumerable<double> SolveQuadratic(double a, double b, double c)
    {
        if (a.Near(0))
        {
            if (!b.Near(0))
                yield return -c / b;

            yield break;
        }

        double discriminant = b * b - 4 * a * c;

        if (discriminant < 0)
            yield break;

        double sqrtDiscriminant = Math.Sqrt(discriminant);

        yield return (-b - sqrtDiscriminant) / (2 * a);
        yield return (-b + sqrtDiscriminant) / (2 * a);
    }
}
