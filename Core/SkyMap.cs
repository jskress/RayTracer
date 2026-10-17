using RayTracer.Basics;

namespace RayTracer.Core;

/// <summary>
/// This class holds how the light of a sky is spread over the directions it comes from, so that the
/// sky may be looked at mostly where its light is.
/// <para>
/// **A photograph of a sky is mostly a sun.**  The sun is a disc half a degree across, a hundred
/// thousandth of the sky, and in a picture of a clear day it carries nine parts in ten of the light.
/// Looked at evenly, as a smooth sky can be, it is almost never found -- and the one sample in a few
/// thousand that does find it brings back a hundred times what the rest of the sky gives.  So the sky is
/// laid out on a grid of directions, each cell weighed by the light from it, and a direction is picked
/// by those weights: a cell first, in proportion to its share of the whole, then a place within it.
/// </para>
/// <para>
/// The grid runs round the sky one way and from straight up to straight down the other, so its cells
/// near the poles are narrow, and each is weighed by the sky it really covers as well as by its light.
/// How likely a direction is to be picked is then known exactly, which is what lets samples picked this
/// way be counted fairly beside samples picked any other way.
/// </para>
/// </summary>
public sealed class SkyMap
{
    /// <summary>
    /// This property holds how many cells the grid has round the sky.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// This property holds how many cells it has from straight up to straight down.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// This property reports whether the sky has any light in it at all.  A black sky has nothing to be
    /// looked at for.
    /// </summary>
    public bool IsDark => _total <= 0;

    // Each cell's weight, row by row; each row's running total of them, ending in one; the running total
    // of the rows' weights, ending in one; and the whole.
    private readonly float[] _weights;
    private readonly float[] _rowRunning;
    private readonly double[] _running;
    private readonly double _total;

    private readonly double _cellAround;
    private readonly double _cellDown;

    private SkyMap(int width, int height, float[] weights)
    {
        Width = width;
        Height = height;

        _weights = weights;
        _rowRunning = new float[width * height];
        _running = new double[height + 1];
        _cellAround = 2 * Math.PI / width;
        _cellDown = Math.PI / height;

        for (int row = 0; row < height; row++)
        {
            double sum = 0;

            for (int column = 0; column < width; column++)
            {
                sum += weights[row * width + column];
                _rowRunning[row * width + column] = (float) sum;
            }

            if (sum > 0)
            {
                for (int column = 0; column < width; column++)
                    _rowRunning[row * width + column] = (float) (_rowRunning[row * width + column] / sum);
            }

            _running[row + 1] = _running[row] + sum;
        }

        _total = _running[height];

        if (_total > 0)
        {
            for (int row = 1; row <= height; row++)
                _running[row] /= _total;
        }
    }

    /// <summary>
    /// This method lays a sky out on a grid of directions, weighing each cell by how bright the sky is
    /// there and by how much of the sky the cell covers.
    /// </summary>
    /// <param name="brightness">How bright the sky is over a cell, given the direction of its middle and
    /// the two edges of the patch of sky it covers, across and down.</param>
    /// <param name="width">How many cells to have round the sky; half as many are had from top to
    /// bottom.</param>
    /// <returns>The map.</returns>
    public static SkyMap Of(Func<Vector, Vector, Vector, double> brightness, int width)
    {
        width = Math.Max(4, width);

        int height = Math.Max(2, width / 2);
        float[] weights = new float[width * height];
        double around = 2 * Math.PI / width;
        double down = Math.PI / height;

        Parallel.For(0, height, row =>
        {
            double polar = (row + 0.5) * down;
            double sine = Math.Sin(polar);

            for (int column = 0; column < width; column++)
            {
                double azimuth = (column + 0.5) * around;
                Vector middle = DirectionAt(polar, azimuth);
                Vector across = new Vector(-Math.Sin(azimuth), 0, Math.Cos(azimuth)) * (around * sine);
                Vector downward = new Vector(
                    Math.Cos(polar) * Math.Cos(azimuth), -Math.Sin(polar), Math.Cos(polar) * Math.Sin(azimuth)) * down;
                double light = brightness(middle, across, downward);

                weights[row * width + column] = (float) (Math.Max(0, light) * sine);
            }
        });

        return new SkyMap(width, height, weights);
    }

    /// <summary>
    /// This method picks a direction by the sky's light.
    /// </summary>
    /// <param name="first">Which row of the grid, as a share from nothing up to one.</param>
    /// <param name="second">Where along the row, as a share from nothing up to one.</param>
    /// <returns>The direction picked.</returns>
    public Vector Pick(double first, double second)
    {
        first = Math.Clamp(first, 0, 1 - 1e-12);
        second = Math.Clamp(second, 0, 1 - 1e-12);

        int row = Search(_running, 0, Height, first);
        double rowShare = _running[row + 1] - _running[row];
        double withinRow = rowShare > 0 ? (first - _running[row]) / rowShare : 0.5;

        int start = row * Width;
        int column = Search(_rowRunning, start, Width, second);
        double before = column == 0 ? 0 : _rowRunning[start + column - 1];
        double cellShare = _rowRunning[start + column] - before;
        double withinCell = cellShare > 0 ? (second - before) / cellShare : 0.5;

        return DirectionAt((row + withinRow) * _cellDown, (column + withinCell) * _cellAround);
    }

    /// <summary>
    /// This method returns how likely a direction is to be picked, per unit of solid angle.
    /// </summary>
    /// <param name="direction">The direction, which must be a unit vector.</param>
    /// <returns>The odds of picking it.</returns>
    public double OddsOf(Vector direction)
    {
        if (_total <= 0)
            return 0;

        double polar = Math.Acos(Math.Clamp(direction.Y, -1, 1));
        double azimuth = Math.Atan2(direction.Z, direction.X);

        if (azimuth < 0)
            azimuth += 2 * Math.PI;

        int row = Math.Min(Height - 1, (int) (polar / _cellDown));
        int column = Math.Min(Width - 1, (int) (azimuth / _cellAround));
        double sine = Math.Sin(polar);

        // A place within a cell is picked evenly in its two angles, so the odds per unit of solid angle
        // rise as the cell narrows toward a pole -- by exactly the sine the cell's weight was given.
        return sine <= 0
            ? 0
            : _weights[row * Width + column] / _total / (_cellAround * _cellDown * sine);
    }

    /// <summary>
    /// This method returns the direction at the given angles: down from straight up, and round from +X
    /// toward +Z, as the spherical image map reads them.
    /// </summary>
    private static Vector DirectionAt(double polar, double azimuth)
    {
        double sine = Math.Sin(polar);

        return new Vector(sine * Math.Cos(azimuth), Math.Cos(polar), sine * Math.Sin(azimuth));
    }

    /// <summary>
    /// This method finds the first of a run of running totals that a share falls below.
    /// </summary>
    private static int Search(float[] running, int start, int count, double share)
    {
        int low = 0, high = count - 1;

        while (low < high)
        {
            int middle = (low + high) / 2;

            if (running[start + middle] > share)
                high = middle;
            else
                low = middle + 1;
        }

        return low;
    }

    /// <summary>
    /// This method finds the row of the grid a share of the whole falls in, by the rows' running totals,
    /// which start at nought.
    /// </summary>
    private static int Search(double[] running, int start, int count, double share)
    {
        int low = 0, high = count - 1;

        while (low < high)
        {
            int middle = (low + high) / 2;

            if (running[start + middle + 1] > share)
                high = middle;
            else
                low = middle + 1;
        }

        return low;
    }
}
