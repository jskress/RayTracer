namespace RayTracer.Geometry;

/// <summary>
/// This class is a height field's ground while it is being worn: its heights in the world, a grid of points spaced
/// as the field is in the world, and the work every kind of wearing shares -- which of a point's eight neighbours
/// lies how far off, which places are the sea, and which way water leaves every hollow.
/// <para>
/// The heights are kept one row after another, a row running along X; a point's number is its row times the
/// columns plus its column.
/// </para>
/// </summary>
internal sealed partial class ErodingGround
{
    // A point's eight neighbours, as steps along X and Z, the four square on first.
    private static readonly int[] StepX = [-1, 1, 0, 0, -1, 1, -1, 1];
    private static readonly int[] StepZ = [0, 0, -1, 1, -1, -1, 1, 1];

    private readonly int _columns;
    private readonly int _rows;
    private readonly int _count;
    private readonly double _width;
    private readonly double _depth;
    private readonly double _scale;

    // The spacing of the grid's points in the world, along X and Z, and how far off each neighbour lies.
    private readonly double _dx;
    private readonly double _dz;
    private readonly double[] _run = new double[8];

    // Which points lie on the grid's edge, which are where water leaves the land, and how far along the numbering
    // each of a point's neighbours lies, for one off the edge.
    private readonly bool[] _edge;
    private readonly bool[] _outlet;
    private readonly int[] _offset = new int[8];

    // The heights in the world, as they were and as they are now.
    private readonly double[] _before;
    private readonly double[] _ground;

    // The level of the sea in the world, or minus infinity for none.
    private readonly double _sea;

    // How far the ground rises from its lowest point to its highest, as first given.
    private readonly double _relief;

    // How far a filled hollow rises from each point to the next, so water runs across it: a trillionth of the land's
    // height, or for ground far off nought, a little more than its heights can tell apart.
    private double Rise => Math.Max(
        Math.Max(_relief, 1e-300) * 1e-12, 4 * Math.Max(Math.Abs(_ground.Max()), Math.Abs(_ground.Min())) * 1.1e-16);

    // How much deeper than a filled hollow rises across itself the water must stand for the hollow to be a lake.
    private double LakeDepth => Math.Max(_relief, 1e-300) * 1e-8;

    /// <summary>
    /// This constructor takes a field's grid of heights in hand.
    /// </summary>
    /// <param name="heights">The heights, in the field's own units, indexed by column and row.</param>
    /// <param name="width">How wide the field is in the world, along X.</param>
    /// <param name="scale">How tall one of the field's own units of height is in the world.</param>
    /// <param name="depth">How deep the field is in the world, along Z.</param>
    /// <param name="seaLevel">The level of the sea in the field's own units, if there is one.</param>
    internal ErodingGround(double[,] heights, double width, double scale, double depth, double? seaLevel)
    {
        _columns = heights.GetLength(0);
        _rows = heights.GetLength(1);
        _count = _columns * _rows;
        _width = width;
        _depth = depth;
        _scale = scale;
        _dx = width / (_columns - 1);
        _dz = depth / (_rows - 1);
        _sea = seaLevel is null ? double.NegativeInfinity : seaLevel.Value * scale;
        _before = new double[_count];
        _ground = new double[_count];

        for (int row = 0; row < _rows; row++)
        {
            for (int column = 0; column < _columns; column++)
                _ground[row * _columns + column] = heights[column, row] * scale;
        }

        Array.Copy(_ground, _before, _count);

        for (int way = 0; way < 8; way++)
        {
            _run[way] = Math.Sqrt(StepX[way] * _dx * StepX[way] * _dx + StepZ[way] * _dz * StepZ[way] * _dz);
            _offset[way] = StepZ[way] * _columns + StepX[way];
        }

        _edge = new bool[_count];
        _outlet = new bool[_count];

        for (int point = 0; point < _count; point++)
        {
            int column = point % _columns;
            int row = point / _columns;

            _edge[point] = column == 0 || row == 0 || column == _columns - 1 || row == _rows - 1;
        }

        _relief = _ground.Max() - _ground.Min();
    }

    /// <summary>
    /// This method returns the number of a point's neighbour the given way, or minus one if it lies off the grid.
    /// A point inside the grid's edge has all eight, a fixed step off in the numbering.
    /// </summary>
    /// <param name="point">The point's number.</param>
    /// <param name="way">Which of the eight ways to look.</param>
    /// <returns>The neighbour's number, or minus one.</returns>
    private int NeighborOf(int point, int way)
    {
        if (!_edge[point])
            return point + _offset[way];

        int column = point % _columns + StepX[way];
        int row = point / _columns + StepZ[way];

        return column < 0 || column >= _columns || row < 0 || row >= _rows ? -1 : row * _columns + column;
    }

    /// <summary>
    /// This method notes whether water leaves the land at a point: it is on the field's edge, or under the sea, as
    /// the ground stood when the outlets were last marked (<see cref="MarkTheOutlets"/>).
    /// </summary>
    /// <param name="point">The point's number.</param>
    /// <returns><c>true</c>, if water leaves there.</returns>
    private bool IsAnOutlet(int point) => _outlet[point];

    /// <summary>
    /// This method marks where water leaves the land as the ground stands: the field's edge, and the sea.
    /// </summary>
    private void MarkTheOutlets()
    {
        for (int point = 0; point < _count; point++)
            _outlet[point] = _edge[point] || _ground[point] <= _sea;
    }

    /// <summary>
    /// This method works out the level water would stand at over every point, if every hollow filled to where it
    /// spills over: Barnes' priority-flood (2014), from every outlet inward, lowest first, each point reached raised
    /// to a hair over the one it was reached from if it lies lower.  So every point drains, and a lake's points stand
    /// under its surface.
    /// <para>
    /// The points come out lowest first, so a point's way down always comes before it: walked forward, the order
    /// meets every river's mouth before its sources, and walked back, every source before its mouth.
    /// </para>
    /// </summary>
    /// <param name="order">The points, in the order they were reached.</param>
    /// <returns>The level water stands at over every point.</returns>
    private double[] Flooded(out int[] order)
    {
        double[] level = new double[_count];
        bool[] reached = new bool[_count];
        double rise = Rise;
        PriorityQueue<int, double> queue = new (2 * (_columns + _rows));
        int next = 0;

        order = new int[_count];

        for (int point = 0; point < _count; point++)
        {
            if (IsAnOutlet(point))
            {
                level[point] = _ground[point];
                reached[point] = true;
                queue.Enqueue(point, level[point]);
            }
        }

        while (queue.TryDequeue(out int point, out double height))
        {
            order[next++] = point;

            for (int way = 0; way < 8; way++)
            {
                int neighbor = NeighborOf(point, way);

                if (neighbor < 0 || reached[neighbor])
                    continue;

                reached[neighbor] = true;
                level[neighbor] = Math.Max(_ground[neighbor], height + rise);
                queue.Enqueue(neighbor, level[neighbor]);
            }
        }

        return level;
    }

    /// <summary>
    /// This method finishes the wearing: the heights back in the field's own units, and the maps of what was done.
    /// </summary>
    /// <returns>The ground, worn.</returns>
    internal ErodedGround Finished()
    {
        double[,] heights = new double[_columns, _rows];
        double[] laid = new double[_count];
        double[] taken = new double[_count];

        for (int point = 0; point < _count; point++)
        {
            double change = _ground[point] - _before[point];

            heights[point % _columns, point / _columns] = _ground[point] / _scale;
            laid[point] = Math.Max(change, 0);
            taken[point] = Math.Max(-change, 0);
        }

        return new ErodedGround
        {
            Heights = heights,
            Flow = FlowMap(),
            Deposits = Measured(laid),
            Wear = Measured(taken)
        };
    }

    /// <summary>
    /// This method turns depths laid down or carried away into a map: nought where there was nothing, a half where
    /// there was about as much as anywhere there was any, and rising toward one past that.
    /// </summary>
    /// <param name="depths">The depths, one for each point.</param>
    /// <returns>The map.</returns>
    private float[] Measured(double[] depths)
    {
        float[] map = new float[_count];
        double least = Math.Max(_relief, 1e-12) * 1e-6;
        double sum = 0;
        int count = 0;

        foreach (double depth in depths)
        {
            if (depth > least)
            {
                sum += depth;
                count++;
            }
        }

        if (count == 0)
            return map;

        // The mean, so a half is where about as much was as anywhere any was: 1 - 2^(-depth / mean).
        double mean = sum / count;

        for (int point = 0; point < _count; point++)
        {
            map[point] = depths[point] > least
                ? (float) (1 - Math.Pow(2, -depths[point] / mean))
                : 0;
        }

        return map;
    }

    /// <summary>
    /// This method works out where water runs over the ground: rain falling evenly on every point and running down,
    /// shared among every neighbour lower than where it is by the fourth power of the slope to it (Holmgren 1994) --
    /// so it spreads over an open slope and gathers in a gully, without the one-point-wide lines that sending it all
    /// one way draws -- across lakes as if filled.
    /// <para>
    /// The map is the share of all the land's water running over a point, to the fourth root: a ridge nought, a
    /// gully draining a ten-thousandth of the land a tenth, one draining a hundredth about a third, the river leaving
    /// it one.  A share, not a count of points, so a finer grid reads the same.
    /// </para>
    /// </summary>
    /// <returns>The flow map.</returns>
    internal float[] FlowMap()
    {
        double[] water = WaterRunning();
        float[] map = new float[_count];

        for (int point = 0; point < _count; point++)
            map[point] = (float) Math.Min(Math.Pow(water[point] / _count, 0.25), Math.BitDecrement(1.0));

        return map;
    }

    /// <summary>
    /// This method works out how much water runs over every point, in points' worth of rain, as the flow map
    /// describes it (<see cref="FlowMap"/>).
    /// </summary>
    /// <returns>The water running over every point.</returns>
    private double[] WaterRunning()
    {
        MarkTheOutlets();

        double[] level = Flooded(out int[] order);
        double[] water = new double[_count];
        double[] share = new double[8];

        Array.Fill(water, 1.0);

        for (int index = _count - 1; index >= 0; index--)
        {
            int point = order[index];

            if (IsAnOutlet(point))
                continue;

            double total = 0;

            for (int way = 0; way < 8; way++)
            {
                int neighbor = NeighborOf(point, way);
                double slope = neighbor < 0 ? 0 : (level[point] - level[neighbor]) / _run[way];

                share[way] = slope > 0 ? slope * slope * slope * slope : 0;
                total += share[way];
            }

            if (total <= 0)
                continue;

            for (int way = 0; way < 8; way++)
            {
                if (share[way] > 0)
                    water[NeighborOf(point, way)] += water[point] * share[way] / total;
            }
        }

        LetLakesBeOneWater(level, water);

        return water;
    }

    /// <summary>
    /// This method gives every point of a lake the water that leaves it: running water across a lake as if over the
    /// filled level, its flow across the still surface was a fan of straight lines from where it spills.  A lake is
    /// every place under its water by more than a filled hollow rises across itself, together with those it touches.
    /// </summary>
    /// <param name="level">The level water stands at over every point.</param>
    /// <param name="water">How much water runs over each point, made the lake's where it is under one.</param>
    private void LetLakesBeOneWater(double[] level, double[] water)
    {
        double depth = LakeDepth;
        bool[] seen = new bool[_count];
        List<int> lake = [];
        Stack<int> next = new ();

        for (int start = 0; start < _count; start++)
        {
            if (seen[start] || level[start] - _ground[start] <= depth)
                continue;

            double most = 0;

            lake.Clear();
            next.Push(start);
            seen[start] = true;

            while (next.TryPop(out int point))
            {
                lake.Add(point);
                most = Math.Max(most, water[point]);

                for (int way = 0; way < 8; way++)
                {
                    int neighbor = NeighborOf(point, way);

                    if (neighbor >= 0 && !seen[neighbor] && level[neighbor] - _ground[neighbor] > depth)
                    {
                        seen[neighbor] = true;
                        next.Push(neighbor);
                    }
                }
            }

            foreach (int point in lake)
                water[point] = most;
        }
    }
}
