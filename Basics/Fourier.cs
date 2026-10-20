using System.Numerics;

namespace RayTracer.Basics;

/// <summary>
/// This class carries out the fast Fourier transform over a grid of values, both ways: Cooley and Tukey's,
/// halving the grid's sides at every step, so each must be a power of two.
/// <para>
/// It is what turns a lens's opening into the pattern light spreads into through it, and what spreads a
/// whole picture's light by that pattern at once: done pixel by pixel, the spreading would cost the
/// picture's size times the pattern's.
/// </para>
/// </summary>
public static class Fourier
{
    /// <summary>
    /// This method transforms a grid in place, rows and then columns.  The way back divides by the grid's
    /// size, so that a grid taken there and back is the grid it was.
    /// </summary>
    /// <param name="grid">The grid, row by row.</param>
    /// <param name="width">How many values a row holds; a power of two.</param>
    /// <param name="height">How many rows there are; a power of two.</param>
    /// <param name="back">Whether to transform back rather than forward.</param>
    public static void Transform(Complex[] grid, int width, int height, bool back = false)
    {
        if (!IsAPowerOfTwo(width) || !IsAPowerOfTwo(height) || grid.Length != width * height)
            throw new ArgumentException($"A {width} by {height} grid cannot be transformed; its sides must be powers of two.");

        Complex[] across = Twiddles(width, back);
        Complex[] down = Twiddles(height, back);

        Parallel.For(0, height, row => Transform(grid.AsSpan(row * width, width), across));
        Parallel.For(0, width, column =>
        {
            Complex[] values = new Complex[height];

            for (int row = 0; row < height; row++)
                values[row] = grid[row * width + column];

            Transform(values, down);

            for (int row = 0; row < height; row++)
                grid[row * width + column] = values[row];
        });

        if (back)
        {
            double scale = 1.0 / (width * height);

            for (int index = 0; index < grid.Length; index++)
                grid[index] *= scale;
        }
    }

    /// <summary>
    /// This method returns the smallest power of two that is at least the given number.
    /// </summary>
    public static int PowerOfTwoFrom(int number)
    {
        int power = 1;

        while (power < number)
            power <<= 1;

        return power;
    }

    private static bool IsAPowerOfTwo(int number) => number > 0 && (number & (number - 1)) == 0;

    // The turns each step multiplies by, worked out once for a side rather than built up by repeated
    // multiplication, which drifts.
    private static Complex[] Twiddles(int size, bool back)
    {
        Complex[] twiddles = new Complex[size / 2];
        double sign = back ? 1 : -1;

        for (int index = 0; index < twiddles.Length; index++)
            twiddles[index] = Complex.FromPolarCoordinates(1, sign * 2 * Math.PI * index / size);

        return twiddles;
    }

    private static void Transform(Span<Complex> values, Complex[] twiddles)
    {
        int size = values.Length;

        // Into bit-reversed order, so the butterflies can work in place.
        for (int index = 1, reversed = 0; index < size; index++)
        {
            int bit = size >> 1;

            for (; (reversed & bit) != 0; bit >>= 1)
                reversed ^= bit;

            reversed ^= bit;

            if (index < reversed)
                (values[index], values[reversed]) = (values[reversed], values[index]);
        }

        for (int length = 2; length <= size; length <<= 1)
        {
            int half = length / 2;
            int stride = size / length;

            for (int start = 0; start < size; start += length)
            {
                for (int offset = 0; offset < half; offset++)
                {
                    Complex turned = values[start + offset + half] * twiddles[offset * stride];
                    Complex kept = values[start + offset];

                    values[start + offset] = kept + turned;
                    values[start + offset + half] = kept - turned;
                }
            }
        }
    }
}
