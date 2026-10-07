// The fitting here is ported from pbrt-v4's rgb2spec_opt.cpp and RGBToSpectrumTable, which are
// Copyright(c) 1998-2020 Matt Pharr, Wenzel Jakob, and Greg Humphreys, and licensed under the Apache
// License, Version 2.0 (SPDX: Apache-2.0).  The method is Wenzel Jakob and Johannes Hanika's, from "A
// Low-Dimensional Function Space for Efficient Spectral Upsampling", Computer Graphics Forum 38(2), 2019.

namespace RayTracer.Graphics;

/// <summary>
/// This class turns a color into a spectrum: one smooth curve across the visible range that, carried
/// back through the renderer's own conversion, comes out as the color it was made from.
/// <para>
/// **There are a great many such curves, and the choice matters.**  Three numbers do not say what a
/// surface does at every wavelength, so any curve that comes back as the right color will do as far as
/// the color goes -- but not as far as everything else goes, because two surfaces of the same color can
/// reflect very differently under the same light once that light is not white.  Jakob and Hanika's
/// answer is to take the smoothest plausible curve: a sigmoid of a quadratic in the wavelength, which
/// stays between nothing and one on its own, as a reflectance must, and never wiggles.  Real paints and
/// dyes are smooth like that, which is why it gives believable answers.
/// </para>
/// <para>
/// **Finding the three coefficients for a color is a small optimization**, and too slow to do for each
/// color a render meets.  So it is done once, over a grid of colors, and a color is answered by
/// interpolating the coefficients of its neighbors.  The grid is built for one particular set of bands
/// and one particular conversion back to color -- the renderer's own -- which is what makes a color
/// round trip; pbrt fits against the CIE tables at five nanometers instead, which its renderer, sampling
/// wavelengths at random, needs and this one does not.  Fitting against a handful of bands rather than
/// several hundred samples is also what makes the grid cheap enough to build when a render starts,
/// rather than having to be generated ahead of time and shipped.
/// </para>
/// <para>
/// The grid's layout is pbrt's: three blocks, one for each primary being the largest, each indexed by
/// that largest value (on a scale crowded toward the ends, where the curves change fastest) and by the
/// other two as fractions of it.
/// </para>
/// </summary>
public class RgbToSpectrumTable
{
    /// <summary>
    /// This property holds how many steps the grid has along each of its three axes.
    /// </summary>
    public int Resolution { get; }

    private readonly double[] _forward;
    private readonly double[] _positions;
    private readonly double[] _scale;
    private readonly float[] _coefficients;
    private readonly (double X, double Y, double Z) _whitePoint;

    // The CIE's matrix from linear sRGB to XYZ, which is all the Lab residual needs: it measures how far
    // apart two colors look, and the colors being compared are both sRGB.
    private static readonly double[,] RgbToXyz =
    {
        { 0.412453, 0.357580, 0.180423 },
        { 0.212671, 0.715160, 0.072169 },
        { 0.019334, 0.119193, 0.950227 }
    };

    private const double Epsilon = 1e-4;

    /// <summary>
    /// This constructor builds the grid for one set of bands.
    /// </summary>
    /// <param name="forward">The renderer's conversion from bands to color: three rows, red, green and
    /// blue, of one weight per band, laid out row after row.</param>
    /// <param name="positions">Where each band's wavelength falls across the visible range, from nought
    /// at the short end to one at the long.</param>
    /// <param name="resolution">How many steps the grid has along each axis.</param>
    public RgbToSpectrumTable(double[] forward, double[] positions, int resolution = 64)
    {
        if (forward.Length != 3 * positions.Length)
            throw new ArgumentException("The conversion needs three weights for every band.");

        Resolution = resolution;
        _forward = forward;
        _positions = positions;
        _scale = new double[resolution];
        _coefficients = new float[3 * 3 * resolution * resolution * resolution];
        _whitePoint = ToXyz(1, 1, 1);

        for (int index = 0; index < resolution; index++)
            _scale[index] = SmoothStep(SmoothStep(index / (double) (resolution - 1)));

        Parallel.For(0, 3 * resolution, slice => Fill(slice / resolution, slice % resolution));
    }

    /// <summary>
    /// This method finds the curve for a color, as the three coefficients of the quadratic inside the
    /// sigmoid.  The color's channels must lie between nought and one.
    /// </summary>
    /// <param name="red">The color's red, from nought to one.</param>
    /// <param name="green">The color's green, from nought to one.</param>
    /// <param name="blue">The color's blue, from nought to one.</param>
    /// <returns>The coefficients, highest power first.</returns>
    public (double A, double B, double C) CoefficientsFor(double red, double green, double blue)
    {
        // A gray is a flat curve, which needs no grid: the sigmoid is turned upside down exactly.
        if (red == green && green == blue)
            return (0, 0, (red - 0.5) / Math.Sqrt(red * (1 - red)));

        int largest = red > green ? red > blue ? 0 : 2 : green > blue ? 1 : 2;
        (double z, double first, double second) = largest switch
        {
            0 => (red, green, blue),
            1 => (green, blue, red),
            _ => (blue, red, green)
        };
        int last = Resolution - 1;
        double x = first * last / z;
        double y = second * last / z;
        int xi = Math.Min((int) x, last - 1);
        int yi = Math.Min((int) y, last - 1);
        int zi = IntervalOf(z);
        double dx = x - xi;
        double dy = y - yi;
        double dz = (z - _scale[zi]) / (_scale[zi + 1] - _scale[zi]);

        return (
            Interpolate(largest, xi, yi, zi, dx, dy, dz, 0),
            Interpolate(largest, xi, yi, zi, dx, dy, dz, 1),
            Interpolate(largest, xi, yi, zi, dx, dy, dz, 2));
    }

    /// <summary>
    /// This method gives a sigmoid curve's height at one place across the visible range.
    /// </summary>
    /// <param name="coefficients">The curve's coefficients.</param>
    /// <param name="position">Where across the range, from nought to one.</param>
    /// <returns>The curve's height there, from nought to one.</returns>
    public static double Evaluate((double A, double B, double C) coefficients, double position)
    {
        double x = (coefficients.A * position + coefficients.B) * position + coefficients.C;

        // A pure white or black asks for a sigmoid pushed all the way to one end, which arrives as an
        // infinity, and infinity over infinity is no answer at all.
        return double.IsInfinity(x)
            ? x > 0 ? 1 : 0
            : Sigmoid(x);
    }

    /// <summary>
    /// This method fits one row of the grid: every color with a given largest primary and a given
    /// second fraction, along the whole of the first fraction and the whole of the scale.
    /// </summary>
    /// <param name="largest">Which primary is the largest.</param>
    /// <param name="row">Which step of the second fraction.</param>
    private void Fill(int largest, int row)
    {
        int last = Resolution - 1;
        double y = row / (double) last;
        double[] rgb = new double[3];
        double[] coefficients = new double[3];

        for (int column = 0; column < Resolution; column++)
        {
            double x = column / (double) last;
            int start = Resolution / 5;

            // Walked outward from a little way up the scale in both directions, each fit starting from
            // the one before, which is close: neighboring colors have neighboring curves, and a good
            // starting point is most of what makes Gauss-Newton converge.
            Array.Clear(coefficients);

            for (int step = start; step < Resolution; step++)
                FitAndStore(largest, step, row, column, x, y, rgb, coefficients);

            Array.Clear(coefficients);

            for (int step = start; step >= 0; step--)
                FitAndStore(largest, step, row, column, x, y, rgb, coefficients);
        }
    }

    /// <summary>
    /// This method fits one cell of the grid and stores what it found.
    /// </summary>
    private void FitAndStore(
        int largest, int step, int row, int column, double x, double y, double[] rgb,
        double[] coefficients)
    {
        double b = _scale[step];

        rgb[largest] = b;
        rgb[(largest + 1) % 3] = x * b;
        rgb[(largest + 2) % 3] = y * b;

        GaussNewton(rgb, coefficients);

        int index = 3 * (((largest * Resolution + step) * Resolution + row) * Resolution + column);

        _coefficients[index] = (float) coefficients[0];
        _coefficients[index + 1] = (float) coefficients[1];
        _coefficients[index + 2] = (float) coefficients[2];
    }

    /// <summary>
    /// This method improves a curve's coefficients until the color it comes back as matches the one
    /// wanted, as judged by eye -- in CIE Lab, where a step means about the same to the eye wherever it
    /// is taken.
    /// </summary>
    /// <param name="rgb">The color wanted.</param>
    /// <param name="coefficients">The coefficients to start from, improved in place.</param>
    private void GaussNewton(double[] rgb, double[] coefficients)
    {
        Span<double> residual = stackalloc double[3];
        Span<double> before = stackalloc double[3];
        Span<double> after = stackalloc double[3];
        Span<double> trial = stackalloc double[3];
        Span<double> jacobian = stackalloc double[9];
        Span<double> wanted = stackalloc double[3];

        ToLab(rgb[0], rgb[1], rgb[2], wanted);

        for (int iteration = 0; iteration < 15; iteration++)
        {
            Residual(coefficients, wanted, residual);

            for (int column = 0; column < 3; column++)
            {
                coefficients.CopyTo(trial);
                trial[column] -= Epsilon;
                Residual(trial, wanted, before);
                coefficients.CopyTo(trial);
                trial[column] += Epsilon;
                Residual(trial, wanted, after);

                for (int line = 0; line < 3; line++)
                    jacobian[line * 3 + column] = (after[line] - before[line]) / (2 * Epsilon);
            }

            if (!Solve(jacobian, residual))
                break;

            double r = 0;

            for (int index = 0; index < 3; index++)
            {
                coefficients[index] -= residual[index];
                r += residual[index] * residual[index];
            }

            double most = Math.Max(Math.Max(coefficients[0], coefficients[1]), coefficients[2]);

            if (most > 200)
            {
                for (int index = 0; index < 3; index++)
                    coefficients[index] *= 200 / most;
            }

            if (r < 1e-6)
                break;
        }
    }

    /// <summary>
    /// This method works out how far the color a curve comes back as lies from the color wanted, in
    /// Lab.  pbrt's residual is the step from the current color to the wanted one, and the sign
    /// matters: it is what the solve below subtracts.
    /// </summary>
    private void Residual(ReadOnlySpan<double> coefficients, ReadOnlySpan<double> wanted, Span<double> into)
    {
        double red = 0, green = 0, blue = 0;
        int count = _positions.Length;
        (double, double, double) curve = (coefficients[0], coefficients[1], coefficients[2]);

        for (int band = 0; band < count; band++)
        {
            double height = Evaluate(curve, _positions[band]);

            red += _forward[band] * height;
            green += _forward[count + band] * height;
            blue += _forward[2 * count + band] * height;
        }

        ToLab(red, green, blue, into);

        for (int index = 0; index < 3; index++)
            into[index] = wanted[index] - into[index];
    }

    /// <summary>
    /// This method solves a three by three system in place, by elimination with partial pivoting.
    /// </summary>
    /// <param name="matrix">The system, row after row; destroyed.</param>
    /// <param name="vector">The right-hand side, replaced by the solution.</param>
    /// <returns><c>true</c>, if the system could be solved, or <c>false</c>, if it is singular.</returns>
    private static bool Solve(Span<double> matrix, Span<double> vector)
    {
        for (int pivot = 0; pivot < 3; pivot++)
        {
            int best = pivot;

            for (int row = pivot + 1; row < 3; row++)
            {
                if (Math.Abs(matrix[row * 3 + pivot]) > Math.Abs(matrix[best * 3 + pivot]))
                    best = row;
            }

            if (Math.Abs(matrix[best * 3 + pivot]) < 1e-15)
                return false;

            if (best != pivot)
            {
                for (int column = 0; column < 3; column++)
                {
                    (matrix[pivot * 3 + column], matrix[best * 3 + column]) =
                        (matrix[best * 3 + column], matrix[pivot * 3 + column]);
                }

                (vector[pivot], vector[best]) = (vector[best], vector[pivot]);
            }

            for (int row = pivot + 1; row < 3; row++)
            {
                double factor = matrix[row * 3 + pivot] / matrix[pivot * 3 + pivot];

                for (int column = pivot; column < 3; column++)
                    matrix[row * 3 + column] -= factor * matrix[pivot * 3 + column];

                vector[row] -= factor * vector[pivot];
            }
        }

        for (int row = 2; row >= 0; row--)
        {
            double sum = vector[row];

            for (int column = row + 1; column < 3; column++)
                sum -= matrix[row * 3 + column] * vector[column];

            vector[row] = sum / matrix[row * 3 + row];
        }

        return true;
    }

    /// <summary>
    /// This method turns a linear sRGB color into CIE Lab, measured against the renderer's white.
    /// </summary>
    private void ToLab(double red, double green, double blue, Span<double> into)
    {
        (double x, double y, double z) = ToXyz(red, green, blue);
        double fx = LabCurve(x / _whitePoint.X);
        double fy = LabCurve(y / _whitePoint.Y);
        double fz = LabCurve(z / _whitePoint.Z);

        into[0] = 116 * fy - 16;
        into[1] = 500 * (fx - fy);
        into[2] = 200 * (fy - fz);
    }

    /// <summary>
    /// This method turns a linear sRGB color into XYZ.
    /// </summary>
    private static (double X, double Y, double Z) ToXyz(double red, double green, double blue)
    {
        return (
            RgbToXyz[0, 0] * red + RgbToXyz[0, 1] * green + RgbToXyz[0, 2] * blue,
            RgbToXyz[1, 0] * red + RgbToXyz[1, 1] * green + RgbToXyz[1, 2] * blue,
            RgbToXyz[2, 0] * red + RgbToXyz[2, 1] * green + RgbToXyz[2, 2] * blue);
    }

    /// <summary>
    /// This method gives Lab's cube root, which is straightened out near nought so that it has a slope
    /// there rather than standing vertical.
    /// </summary>
    private static double LabCurve(double t)
    {
        const double delta = 6.0 / 29.0;

        return t > delta * delta * delta
            ? Math.Cbrt(t)
            : t / (delta * delta * 3) + 4.0 / 29.0;
    }

    /// <summary>
    /// This method finds which step of the scale a largest primary falls in.
    /// </summary>
    private int IntervalOf(double z)
    {
        int low = 0;
        int high = Resolution - 1;

        // The last step whose start the value has reached, kept to one that has a step after it.
        while (high - low > 1)
        {
            int middle = (low + high) / 2;

            if (_scale[middle] < z)
                low = middle;
            else
                high = middle;
        }

        return Math.Clamp(low, 0, Resolution - 2);
    }

    /// <summary>
    /// This method interpolates one coefficient across the eight grid points around a color.
    /// </summary>
    private double Interpolate(
        int largest, int xi, int yi, int zi, double dx, double dy, double dz, int which)
    {
        int resolution = Resolution;

        double At(int x, int y, int z)
        {
            int cell = ((largest * resolution + zi + z) * resolution + yi + y) * resolution + xi + x;

            return _coefficients[3 * cell + which];
        }

        return Lerp(dz,
            Lerp(dy, Lerp(dx, At(0, 0, 0), At(1, 0, 0)), Lerp(dx, At(0, 1, 0), At(1, 1, 0))),
            Lerp(dy, Lerp(dx, At(0, 0, 1), At(1, 0, 1)), Lerp(dx, At(0, 1, 1), At(1, 1, 1))));
    }

    private static double Lerp(double t, double from, double to) => (1 - t) * from + t * to;

    private static double Sigmoid(double x) => 0.5 * x / Math.Sqrt(1 + x * x) + 0.5;

    private static double SmoothStep(double x) => x * x * (3 - 2 * x);
}
