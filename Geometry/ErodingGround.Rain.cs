namespace RayTracer.Geometry;

/// <summary>
/// This part of a ground being worn lets rain run down it.
/// </summary>
internal sealed partial class ErodingGround
{
    // The drop's numbers, Beyer's (2015) as Lague's widely copied version of it has them, for a grid 256 points
    // across with heights running nought to one: how far a drop keeps the way it was going, how much grit it can
    // carry for its speed, its water and the fall under it, the least it can carry, how much of the difference it
    // wears or lays down in a step, how much of its water is left after one, how fast a fall speeds it, and how many
    // steps it lives.
    private const double Inertia = 0.05;
    private const double CarryingFactor = 4;
    private const double LeastCarried = 0.01;
    private const double WearingShare = 0.3;
    private const double LayingShare = 0.3;
    private const double Evaporation = 0.01;
    private const double Gravity = 4;
    private const int Lifetime = 30;
    private const double FinestGrid = 256;

    // How far about the place a drop is it wears the ground, in points of the grid.
    private const int WearingRadius = 3;

    // How many points the grid rain falls on has along the land's longer side.
    private const int RainGrid = 512;

    // The least share of the land a place drains for its water to be a river, which carries off what a drop brings.
    private const double RiverShare = 0.01;

    /// <summary>
    /// This method lets rain fall on the land (<see cref="RainOnTheGrid"/>), always on a grid of its own
    /// <see cref="RainGrid"/> points along the land's longer side, whatever the field's own grid: what it wore away
    /// and laid down is then laid back on the field's grid, averaged over each point's share of it where the field's
    /// is the coarser and read between its points where the field's is the finer.
    /// <para>
    /// A drop wears a few points of its grid about it, so on the field's own grid a finer grid wore gullies as
    /// much narrower, and -- carrying as much grit -- as much deeper: the land came out another land at every
    /// sampling.  On a grid of its own it wears the same gullies however finely the field is drawn, and costs the
    /// same.
    /// </para>
    /// </summary>
    /// <param name="perPoint">How many drops fall for each point of the rain's grid.</param>
    /// <param name="seed">The seed the drops' places are drawn by.</param>
    internal void FallAsRain(double perPoint, int seed)
    {
        if (_relief <= 0)
            return;

        double longest = Math.Max(_width, _depth);
        int columns = Math.Max(2, (int) Math.Round(RainGrid * _width / longest) + 1);
        int rows = Math.Max(2, (int) Math.Round(RainGrid * _depth / longest) + 1);

        if (columns == _columns && rows == _rows)
        {
            RainOnTheGrid(perPoint, seed);

            return;
        }

        double[,] heights = new double[columns, rows];
        double acrossX = (_columns - 1.0) / (columns - 1);
        double acrossZ = (_rows - 1.0) / (rows - 1);

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
                heights[column, row] = ReadBetween(_ground, _columns, _rows, column * acrossX, row * acrossZ);
        }

        ErodingGround rain = new (
            heights, _width, 1, _depth, double.IsNegativeInfinity(_sea) ? null : _sea);

        rain.RainOnTheGrid(perPoint, seed);

        double[] change = new double[rain._count];

        for (int point = 0; point < rain._count; point++)
            change[point] = rain._ground[point] - rain._before[point];

        // Back on the field's grid: each of its points the average over its share of the rain's, or read between.
        double halfX = 0.5 / acrossX;
        double halfZ = 0.5 / acrossZ;

        Parallel.For(0, _rows, row =>
        {
            for (int column = 0; column < _columns; column++)
            {
                double x = column / acrossX;
                double z = row / acrossZ;

                _ground[row * _columns + column] += halfX > 0.5 || halfZ > 0.5
                    ? AverageOver(change, columns, rows, x, z, halfX, halfZ)
                    : ReadBetween(change, columns, rows, x, z);
            }
        });
    }

    /// <summary>
    /// This method reads a grid's values between its points, by bilinear interpolation.
    /// </summary>
    /// <param name="values">The values, row by row.</param>
    /// <param name="columns">How many columns the grid has.</param>
    /// <param name="rows">How many rows it has.</param>
    /// <param name="x">The column to read at, with its fraction.</param>
    /// <param name="z">The row to read at, with its fraction.</param>
    /// <returns>The value there.</returns>
    private static double ReadBetween(double[] values, int columns, int rows, double x, double z)
    {
        int column = Math.Clamp((int) x, 0, columns - 2);
        int row = Math.Clamp((int) z, 0, rows - 2);
        double u = Math.Clamp(x - column, 0, 1);
        double v = Math.Clamp(z - row, 0, 1);
        int corner = row * columns + column;

        return values[corner] * (1 - u) * (1 - v) + values[corner + 1] * u * (1 - v) +
               values[corner + columns] * (1 - u) * v + values[corner + columns + 1] * u * v;
    }

    /// <summary>
    /// This method averages a grid's values over the points within a box about a place.
    /// </summary>
    /// <param name="values">The values, row by row.</param>
    /// <param name="columns">How many columns the grid has.</param>
    /// <param name="rows">How many rows it has.</param>
    /// <param name="x">The column of the box's middle, with its fraction.</param>
    /// <param name="z">The row of the box's middle, with its fraction.</param>
    /// <param name="halfX">Half the box's width, in columns.</param>
    /// <param name="halfZ">Half the box's height, in rows.</param>
    /// <returns>The average.</returns>
    private static double AverageOver(
        double[] values, int columns, int rows, double x, double z, double halfX, double halfZ)
    {
        int left = Math.Max((int) Math.Ceiling(x - halfX), 0);
        int right = Math.Min((int) Math.Floor(x + halfX), columns - 1);
        int top = Math.Max((int) Math.Ceiling(z - halfZ), 0);
        int bottom = Math.Min((int) Math.Floor(z + halfZ), rows - 1);
        double sum = 0;
        int count = 0;

        for (int row = top; row <= bottom; row++)
        {
            for (int column = left; column <= right; column++)
            {
                sum += values[row * columns + column];
                count++;
            }
        }

        return count > 0 ? sum / count : ReadBetween(values, columns, rows, x, z);
    }

    /// <summary>
    /// This method lets rain fall on this grid, a drop at a time, each from a place of its own, running down the
    /// ground as Beyer (2015) has it: it keeps a little of the way it was going and turns the rest toward the way
    /// down, speeds up as it falls, and can carry more grit the faster it goes, the more water it has and the
    /// steeper it falls.  Carrying less than it could, it wears the ground about it; more, or climbing, it lays some
    /// down; and it dries as it goes.  So gullies cut, grit fans out where a slope eases, and valley floors fill flat.
    /// <para>
    /// The heights it wears are the land's, run nought to one from its lowest to its highest, so a land twice as tall
    /// wears to the same shape.  A grid finer than 256 points across has every drop live as many more steps as
    /// it takes to run as far, lose as much water and wear and lay down as much over that run, and carry as much: so
    /// a finer grid wears gullies as deep, though narrower, its drops wearing a radius of three points about them.
    /// A drop running off the grid takes its grit with it; one reaching the sea lays all it carries there; and one
    /// reaching a river -- a place draining a hundredth of the land, as the ground stood when the rain began -- is
    /// carried off down it with all it carries.  Left to lay its grit down there, every drop off a valley's walls
    /// filled its floor, and three drops a point buried a valley 2.8 kilometers deep by hundreds of meters.
    /// </para>
    /// </summary>
    /// <param name="perPoint">How many drops fall for each point of the grid.</param>
    /// <param name="seed">The seed the drops' places are drawn by.</param>
    private void RainOnTheGrid(double perPoint, int seed)
    {
        double low = _ground.Min();
        double sea = (_sea - low) / _relief;
        double across = Math.Sqrt((_columns - 1.0) * (_rows - 1.0));
        double fineness = Math.Max(1, across / FinestGrid);
        int life = (int) Math.Round(Lifetime * fineness);
        double carrying = CarryingFactor * fineness;
        double wearing = 1 - Math.Pow(1 - WearingShare, 1 / fineness);
        double laying = 1 - Math.Pow(1 - LayingShare, 1 / fineness);
        double kept = Math.Pow(1 - Evaporation, 1 / fineness);
        double step = Math.Min(_dx, _dz);
        long drops = (long) Math.Round(perPoint * _count);
        double[] height = new double[_count];
        (int Column, int Row, double Weight)[] brush = BrushOf(WearingRadius);

        for (int point = 0; point < _count; point++)
            height[point] = (_ground[point] - low) / _relief;

        // Where the rivers run, before any rain wears the ground.
        double[] running = WaterRunning();
        bool[] river = new bool[_count];

        for (int point = 0; point < _count; point++)
            river[point] = running[point] >= RiverShare * _count;

        for (long drop = 0; drop < drops; drop++)
        {
            double x = Field.DrawFor(seed, (int) (drop >> 31), (int) (drop & 0x7FFFFFFF), 1) * (_columns - 1);
            double z = Field.DrawFor(seed, (int) (drop >> 31), (int) (drop & 0x7FFFFFFF), 2) * (_rows - 1);
            double wayX = 0;
            double wayZ = 0;
            double speed = 1;
            double water = 1;
            double carried = 0;

            for (int age = 0; age < life; age++)
            {
                int column = (int) x;
                int row = (int) z;
                double u = x - column;
                double v = z - row;
                (double here, double slopeX, double slopeZ) = HeightAndSlopeAt(height, x, z);

                // Its way, a little of what it was and the rest down the slope, in the world.
                wayX = wayX * Inertia - slopeX / _dx * (1 - Inertia);
                wayZ = wayZ * Inertia - slopeZ / _dz * (1 - Inertia);

                double length = Math.Sqrt(wayX * wayX + wayZ * wayZ);

                if (length <= 0)
                    break;

                wayX /= length;
                wayZ /= length;
                x += wayX * step / _dx;
                z += wayZ * step / _dz;

                if (x < 0 || z < 0 || x >= _columns - 1 || z >= _rows - 1)
                    break;

                double there = HeightAndSlopeAt(height, x, z).Height;
                double fall = there - here;

                if (there <= sea)
                {
                    LayDown(height, column, row, u, v, carried);
                    break;
                }

                if (river[(int) Math.Round(z) * _columns + (int) Math.Round(x)])
                    break;

                double capacity = Math.Max(-fall * speed * water * carrying, LeastCarried);

                if (carried > capacity || fall > 0)
                {
                    double laid = fall > 0 ? Math.Min(fall, carried) : (carried - capacity) * laying;

                    carried -= laid;
                    LayDown(height, column, row, u, v, laid);
                }
                else
                {
                    carried += WearAway(
                        height, column, row, brush, Math.Min((capacity - carried) * wearing, -fall), there);
                }

                speed = Math.Sqrt(Math.Max(speed * speed - fall * Gravity, 0));
                water *= kept;
            }
        }

        for (int point = 0; point < _count; point++)
            _ground[point] = low + height[point] * _relief;
    }

    /// <summary>
    /// This method returns the height of the ground at a place between the points of the grid, by bilinear
    /// interpolation, and its slope along each axis per point of the grid.
    /// </summary>
    /// <param name="height">The heights.</param>
    /// <param name="x">The place's column, with its fraction.</param>
    /// <param name="z">The place's row, with its fraction.</param>
    /// <returns>The height there, and its slopes.</returns>
    private (double Height, double SlopeX, double SlopeZ) HeightAndSlopeAt(double[] height, double x, double z)
    {
        int column = (int) x;
        int row = (int) z;
        double u = x - column;
        double v = z - row;
        int corner = row * _columns + column;
        double nw = height[corner];
        double ne = height[corner + 1];
        double sw = height[corner + _columns];
        double se = height[corner + _columns + 1];

        return (
            nw * (1 - u) * (1 - v) + ne * u * (1 - v) + sw * (1 - u) * v + se * u * v,
            (ne - nw) * (1 - v) + (se - sw) * v,
            (sw - nw) * (1 - u) + (se - ne) * u);
    }

    /// <summary>
    /// This method lays grit down on the four points round a place, by how near it is to each.
    /// </summary>
    /// <param name="height">The heights.</param>
    /// <param name="column">The column of the place's corner.</param>
    /// <param name="row">The row of the place's corner.</param>
    /// <param name="u">How far along the cell the place is.</param>
    /// <param name="v">How far down the cell the place is.</param>
    /// <param name="amount">How much grit to lay down.</param>
    private void LayDown(double[] height, int column, int row, double u, double v, double amount)
    {
        int corner = row * _columns + column;

        height[corner] += amount * (1 - u) * (1 - v);
        height[corner + 1] += amount * u * (1 - v);
        height[corner + _columns] += amount * (1 - u) * v;
        height[corner + _columns + 1] += amount * u * v;
    }

    /// <summary>
    /// This method wears the ground away about a point, more near it and less out to the brush's edge, the weights
    /// made up again where the brush runs off the grid -- but no point below where the drop goes next.  Worn
    /// further, a hole by a drop's path deepened itself: the next drop fell into it faster, carried more and wore it
    /// deeper still, until the ground was tens of thousands of times as deep as it was tall.
    /// </summary>
    /// <param name="height">The heights.</param>
    /// <param name="column">The point's column.</param>
    /// <param name="row">The point's row.</param>
    /// <param name="brush">The points about it to wear, and by how much of the amount.</param>
    /// <param name="amount">How much to wear away.</param>
    /// <param name="floor">The height of where the drop goes next, which nothing is worn below.</param>
    /// <returns>How much was worn away.</returns>
    private double WearAway(
        double[] height, int column, int row, (int Column, int Row, double Weight)[] brush, double amount,
        double floor)
    {
        bool inside = column >= WearingRadius && row >= WearingRadius &&
                      column < _columns - WearingRadius && row < _rows - WearingRadius;
        double total = 1;

        if (!inside)
        {
            total = 0;

            foreach ((int dc, int dr, double weight) in brush)
            {
                if (IsOnTheGrid(column + dc, row + dr))
                    total += weight;
            }
        }

        double worn = 0;

        foreach ((int dc, int dr, double weight) in brush)
        {
            if (!inside && !IsOnTheGrid(column + dc, row + dr))
                continue;

            int point = (row + dr) * _columns + column + dc;
            double taken = Math.Min(amount * weight / total, Math.Max(height[point] - floor, 0));

            height[point] -= taken;
            worn += taken;
        }

        return worn;
    }

    /// <summary>
    /// This method notes whether a column and row lie on the grid.
    /// </summary>
    private bool IsOnTheGrid(int column, int row) => column >= 0 && row >= 0 && column < _columns && row < _rows;

    /// <summary>
    /// This method makes the brush a drop wears the ground with: every point within the radius of it, weighed by how
    /// far inside the radius it is, the weights adding to one.
    /// </summary>
    /// <param name="radius">The brush's radius, in points.</param>
    /// <returns>The brush.</returns>
    private static (int Column, int Row, double Weight)[] BrushOf(int radius)
    {
        List<(int Column, int Row, double Weight)> brush = [];
        double total = 0;

        for (int dr = -radius; dr <= radius; dr++)
        {
            for (int dc = -radius; dc <= radius; dc++)
            {
                double weight = radius - Math.Sqrt(dc * dc + dr * dr);

                if (weight > 0)
                {
                    brush.Add((dc, dr, weight));
                    total += weight;
                }
            }
        }

        return brush.Select(entry => (entry.Column, entry.Row, entry.Weight / total)).ToArray();
    }
}
