using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class holds the sky as seen through the openings in one wall: laid out on a square grid in the wall's
/// own frame, so that an opening seen from anywhere in the room is a rectangle on the grid, and the sky's light
/// through it can be looked at mostly where that light is.
/// <para>
/// **The trick is Bitterli's (Bitterli, Novák and Jarosz 2015, "Portal-masked environment map sampling"), by
/// way of PBRT's <c>PortalImageInfiniteLight</c>.**  A direction is placed on the grid by the two angles it
/// leans at, one in each of the frame's edge directions -- atan2(x, z) and atan2(y, z), z being out through the
/// wall.  Every direction from a point through a rectangle in the wall meets the wall's plane at the same height
/// above the point, so each of those angles hangs on one of the rectangle's sides alone, and the opening's
/// directions are exactly the grid's rectangle between its corners' angles: whatever point it is seen from, it
/// is a window on one and the same grid.
/// </para>
/// <para>
/// Each cell is weighed by the sky's light at its middle times the solid angle the cell covers, and a running
/// total of the weights over every rectangle from the grid's corner (a summed-area table) gives the light in any
/// window at once, cells cut part way across included -- the totals are read between their places, which for a
/// grid of even cells is exact.  A place in a window is picked by halving: across first, by the light in the
/// window's columns, then down the column it fell in.
/// </para>
/// </summary>
internal sealed class PortalMap
{
    /// <summary>
    /// This property holds how many cells the grid has each way.
    /// </summary>
    public int Size { get; }

    // Each cell's weight, row by row (a row running across, along the frame's first edge), and the running
    // totals, one wider each way than the grid and nought along its first row and column: the total at
    // (column, row) is that of every cell before both.
    private readonly float[] _values;
    private readonly double[] _sums;

    private PortalMap(int size, float[] values)
    {
        int stride = size + 1;

        Size = size;
        _values = values;
        _sums = new double[stride * stride];

        for (int row = 0; row < size; row++)
        {
            double inRow = 0;

            for (int column = 0; column < size; column++)
            {
                inRow += values[row * size + column];
                _sums[(row + 1) * stride + column + 1] = _sums[row * stride + column + 1] + inRow;
            }
        }
    }

    /// <summary>
    /// This method lays the sky out on a grid in a wall's frame, weighing each cell by the sky's brightness over it
    /// and by the solid angle it covers.
    /// </summary>
    /// <param name="along">The frame's first direction, along the wall.</param>
    /// <param name="across">Its second, along the wall and square to the first.</param>
    /// <param name="outward">Its third, out through the wall toward the sky.</param>
    /// <param name="brightness">How bright the sky is over a cell, given the direction of its middle and the two
    /// edges of the patch of sky it covers, across and down.</param>
    /// <param name="size">How many cells to have each way.</param>
    /// <returns>The map.</returns>
    public static PortalMap Of(
        Vector along, Vector across, Vector outward, Func<Vector, Vector, Vector, double> brightness, int size)
    {
        size = Math.Max(2, size);

        float[] values = new float[size * size];

        Parallel.For(0, size, row =>
        {
            double v = (row + 0.5) / size;

            for (int column = 0; column < size; column++)
            {
                double u = (column + 0.5) / size;
                Vector local = LocalAt(u, v);
                Vector side = World(LocalAt(u + 0.5 / size, v)) - World(LocalAt(u - 0.5 / size, v));
                Vector down = World(LocalAt(u, v + 0.5 / size)) - World(LocalAt(u, v - 0.5 / size));

                values[row * size + column] = (float) (Math.Max(0, brightness(World(local), side, down)) * Stretch(local));
            }
        });

        return new PortalMap(size, values);

        Vector World(Vector local) => along * local.X + across * local.Y + outward * local.Z;
    }

    /// <summary>
    /// This method returns the sky's light in a window of the grid -- its brightness over the solid angle the
    /// window covers.
    /// </summary>
    /// <param name="u0">The window's first edge across, from nought to one.</param>
    /// <param name="v0">Its first edge down.</param>
    /// <param name="u1">Its far edge across.</param>
    /// <param name="v1">Its far edge down.</param>
    /// <returns>The light in it.</returns>
    public double Integral(double u0, double v0, double u1, double v1)
    {
        double sum = Summed(u1, v1) - Summed(u0, v1) - Summed(u1, v0) + Summed(u0, v0);

        return Math.Max(0, sum / ((double) Size * Size));
    }

    /// <summary>
    /// This method picks a place in a window of the grid by the light in it, or none for a window that holds no
    /// light.
    /// </summary>
    /// <param name="first">Where across, as a share from nothing up to one.</param>
    /// <param name="second">Where down, as a share from nothing up to one.</param>
    /// <param name="u0">The window's first edge across.</param>
    /// <param name="v0">Its first edge down.</param>
    /// <param name="u1">Its far edge across.</param>
    /// <param name="v1">Its far edge down.</param>
    /// <returns>The place, across and down.</returns>
    public (double U, double V)? Pick(double first, double second, double u0, double v0, double u1, double v1)
    {
        double whole = Integral(u0, v0, u1, v1);

        if (whole <= 0)
            return null;

        double u = Halve(across => Integral(u0, v0, across, v1) / whole, first, u0, u1);

        // Down the column of cells the place fell in, the light runs the same way at every place across it, so the
        // whole column is looked down rather than only the part of it inside the window.
        double low = Math.Floor(u * Size) / Size;
        double high = Math.Min(1, low + 1.0 / Size);
        double column = Integral(low, v0, high, v1);

        if (column <= 0)
            return null;

        double v = Halve(down => Integral(low, v0, high, down) / column, second, v0, v1);

        return (u, v);
    }

    /// <summary>
    /// This method returns the weight of the cell a place on the grid falls in, per unit of the grid's area: the
    /// odds of picking the place, times the light in the window it was picked from.
    /// </summary>
    /// <param name="u">The place across.</param>
    /// <param name="v">The place down.</param>
    /// <returns>The weight there.</returns>
    public double ValueAt(double u, double v)
    {
        int column = Math.Clamp((int) (u * Size), 0, Size - 1);
        int row = Math.Clamp((int) (v * Size), 0, Size - 1);

        return _values[row * Size + column];
    }

    /// <summary>
    /// This method returns the direction, in the frame, at a place on the grid.
    /// </summary>
    /// <param name="u">The place across, from nought to one.</param>
    /// <param name="v">The place down.</param>
    /// <returns>The unit direction, its third part out through the wall.</returns>
    public static Vector LocalAt(double u, double v)
    {
        double x = Math.Tan((u - 0.5) * Math.PI);
        double y = Math.Tan((v - 0.5) * Math.PI);

        return new Vector(x, y, 1).Unit;
    }

    /// <summary>
    /// This method returns the place on the grid of a direction in the frame that leans out through the wall.
    /// </summary>
    /// <param name="local">The unit direction, its third part above nought.</param>
    /// <returns>The place across and down.</returns>
    public static (double U, double V) PlaceOf(Vector local) =>
        ((Math.Atan2(local.X, local.Z) + Math.PI / 2) / Math.PI, (Math.Atan2(local.Y, local.Z) + Math.PI / 2) / Math.PI);

    /// <summary>
    /// This method returns how much solid angle a unit of the grid's area covers at a direction: pi squared, for
    /// the two angles a unit of the grid spans, times (1 - x^2)(1 - y^2)/z.
    /// </summary>
    /// <param name="local">The unit direction, in the frame.</param>
    /// <returns>The solid angle per unit of area.</returns>
    public static double Stretch(Vector local) =>
        Math.PI * Math.PI * (1 - local.X * local.X) * (1 - local.Y * local.Y) / local.Z;

    /// <summary>
    /// This method reads the running total at a place on the grid, between the totals at the cells' corners.
    /// </summary>
    private double Summed(double u, double v)
    {
        double x = Math.Clamp(u, 0, 1) * Size;
        double y = Math.Clamp(v, 0, 1) * Size;
        int column = Math.Min((int) x, Size - 1);
        int row = Math.Min((int) y, Size - 1);
        double dx = x - column;
        double dy = y - row;
        int stride = Size + 1;
        int at = row * stride + column;

        return (1 - dx) * (1 - dy) * _sums[at] + dx * (1 - dy) * _sums[at + 1] +
               (1 - dx) * dy * _sums[at + stride] + dx * dy * _sums[at + stride + 1];
    }

    /// <summary>
    /// This method finds where a running share reaches a target between two places, halving until the two lie in
    /// one cell, over which the share runs straight, and reading the place off it there.
    /// </summary>
    private double Halve(Func<double, double> share, double target, double low, double high)
    {
        for (int step = 0; step < 64 && Math.Ceiling(Size * high) - Math.Floor(Size * low) > 1; step++)
        {
            double middle = (low + high) / 2;

            if (share(middle) > target)
                high = middle;
            else
                low = middle;
        }

        double atLow = share(low);
        double atHigh = share(high);
        double along = atHigh > atLow ? (target - atLow) / (atHigh - atLow) : 0.5;

        return Math.Clamp(low + along * (high - low), low, high);
    }
}
