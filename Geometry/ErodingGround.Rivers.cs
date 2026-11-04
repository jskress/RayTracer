namespace RayTracer.Geometry;

/// <summary>
/// This part of a ground being worn cuts its rivers.
/// </summary>
internal sealed partial class ErodingGround
{
    // How many times the ways water runs are found again while the rivers cut: the ground falls between, and the
    // ways change with it.
    private const int RiverSteps = 24;

    /// <summary>
    /// This method cuts the land's rivers by the stream power law: the ground at a place wears down at a rate that
    /// is the slope water leaves it by times the square root of how much land drains through it, so a gully barely
    /// cuts and a river draining half the land cuts deep, and the valleys branch as the water that made them does.
    /// <para>
    /// It is solved as Braun and Willett (2013) solve it, implicitly, mouths first, with the water of a place shared
    /// among all its lower neighbours by the square of the slope to each (as Yuan et al. 2019 share it), so a
    /// river's bed is not one point of the grid wide and an open slope is not scored with parallel grooves: a
    /// place's new height is <c>(h + sum F h') / (1 + sum F)</c>, the h' its ways down's new heights, which is stable
    /// however far a step goes.  The ways down run over the water's level (<see cref="Flooded"/>), so a river runs on
    /// across a lake; a lake's bed lies under still water and is left alone, and where a river comes down into one,
    /// the lake's surface is the level it cuts down to.  The sea and the field's edges are where the rivers end, and
    /// do not wear.
    /// </para>
    /// <para>
    /// The law's rate is said as how far a river cuts back: the rate at a place draining a share A of the land is
    /// <c>reach x sqrt(A) x width</c> over the whole of the wearing, so at reach one a river draining all the land
    /// would have cut its way back one width of it.  The law is linear in height, so a land made twice as tall is cut
    /// to the same shape, twice as tall -- only the sides of its valleys, falling at a true angle, tell the two apart;
    /// and its rates are worked out from areas and lengths in the world, so a finer grid cuts the same land the same
    /// way.
    /// </para>
    /// <para>
    /// A river cuts down only its bed; the sides of the valley it cuts stand until they are too steep to, and then
    /// fall back to the angle loose ground rests at, the rock that fell carried off down the river (the threshold
    /// hillslopes of a range whose rivers cut fast, Burbank et al. 1996).  So a river makes a valley, not a slot.
    /// Ground already steeper than that before the rivers cut -- a crag -- keeps its steepness as it falls back, and
    /// is never made steeper: exempted from falling at all, a crag beside a river rose into a wall one point of the
    /// grid wide as the river cut down at its foot.
    /// </para>
    /// </summary>
    /// <param name="reach">How far the rivers have cut back, as shares of the land's width.</param>
    /// <param name="repose">The steepest angle the sides of a valley stand at.</param>
    internal void CutRivers(double reach, double repose)
    {
        double whole = _width * _depth;
        double width = Math.Sqrt(whole);
        double cell = whole / _count;
        double step = reach / RiverSteps;
        double tan = Math.Tan(repose);
        double[] drained = new double[_count];
        double[] share = new double[8];
        bool[] submerged = new bool[_count];
        double[] uncut = (double[]) _ground.Clone();

        MarkTheOutlets();

        for (int round = 0; round < RiverSteps; round++)
        {
            double[] level = Flooded(out int[] order);

            // How much land drains through each place: its own, and all that drains into it, sources first.
            Array.Fill(drained, cell);

            for (int index = _count - 1; index >= 0; index--)
            {
                int point = order[index];
                double total = SharesDown(level, point, share);

                for (int way = 0; way < 8 && total > 0; way++)
                {
                    if (share[way] > 0)
                        drained[NeighborOf(point, way)] += drained[point] * share[way] / total;
                }
            }

            // Which places lie under a lake, before any is worn: worn, a place lies under its old level.
            for (int point = 0; point < _count; point++)
                submerged[point] = _ground[point] < level[point];

            // Mouths first, so each place's ways down are already worn when the place itself is.
            for (int index = 0; index < _count; index++)
            {
                int point = order[index];
                double total = SharesDown(level, point, share);

                if (total <= 0 || submerged[point])
                    continue;

                double rate = step * Math.Sqrt(drained[point] / whole) * width / total;
                double rates = 0;
                double toward = 0;

                for (int way = 0; way < 8; way++)
                {
                    if (share[way] <= 0)
                        continue;

                    int below = NeighborOf(point, way);
                    double f = rate * share[way] / _run[way];

                    rates += f;
                    toward += f * (submerged[below] ? level[below] : _ground[below]);
                }

                _ground[point] = (_ground[point] + toward) / (1 + rates);
            }

            LetValleySidesFall(tan, uncut);
        }
    }

    /// <summary>
    /// This method works out how a place's water is shared among its lower neighbours: by the square of the slope to
    /// each over the water's level.  An outlet sends none on.
    /// </summary>
    /// <param name="level">The level water stands at over every point.</param>
    /// <param name="point">The place.</param>
    /// <param name="share">Each way's share, filled in, before being divided by their total.</param>
    /// <returns>The shares' total, or nought for a place that sends none on.</returns>
    private double SharesDown(double[] level, int point, double[] share)
    {
        if (IsAnOutlet(point))
            return 0;

        double total = 0;

        for (int way = 0; way < 8; way++)
        {
            int neighbor = NeighborOf(point, way);
            double slope = neighbor < 0 ? 0 : (level[point] - level[neighbor]) / _run[way];

            share[way] = slope > 0 ? slope * slope : 0;
            total += share[way];
        }

        return total;
    }

    /// <summary>
    /// This method lets the sides of the valleys fall back wherever the cutting has left them too steep: from the
    /// lowest place up, a neighbour standing higher over a place than it may (<see cref="Steepest"/>) comes down to
    /// it.  Only what is too steep is visited: it starts from every place with such a neighbour, and goes on from
    /// each neighbour that falls.
    /// </summary>
    /// <param name="tan">The tangent of the angle of repose.</param>
    /// <param name="uncut">The ground as it was before the rivers cut.</param>
    private void LetValleySidesFall(double tan, double[] uncut)
    {
        PriorityQueue<int, double> queue = new ();

        for (int point = 0; point < _count; point++)
        {
            for (int way = 0; way < 8; way++)
            {
                int neighbor = NeighborOf(point, way);

                if (neighbor >= 0 && Falls(point, neighbor, way, tan, uncut))
                {
                    queue.Enqueue(point, _ground[point]);
                    break;
                }
            }
        }

        while (queue.TryDequeue(out int point, out double height))
        {
            if (height != _ground[point])
                continue;

            for (int way = 0; way < 8; way++)
            {
                int neighbor = NeighborOf(point, way);

                if (neighbor < 0 || !Falls(point, neighbor, way, tan, uncut))
                    continue;

                _ground[neighbor] = _ground[point] + _run[way] * Steepest(point, neighbor, way, tan, uncut);
                queue.Enqueue(neighbor, _ground[neighbor]);
            }
        }
    }

    /// <summary>
    /// This method notes whether a place's neighbour stands too steeply over it.  The field's edges and the sea do
    /// not fall.
    /// </summary>
    private bool Falls(int point, int neighbor, int way, double tan, double[] uncut) =>
        _ground[neighbor] > _ground[point] + _run[way] * Steepest(point, neighbor, way, tan, uncut) &&
        !IsAnOutlet(neighbor);

    /// <summary>
    /// This method returns the steepest a neighbour may stand over a place once the rivers have cut: at the angle of
    /// repose, or as steeply as it stood before any river cut, if that was steeper.  So gentle ground cut into falls
    /// back to the angle of repose, and a crag cut under keeps its steepness, falling back as a cliff whose foot is
    /// undercut does; and nothing the rivers did not cut moves at all.
    /// </summary>
    private double Steepest(int point, int neighbor, int way, double tan, double[] uncut) =>
        Math.Max(tan, (uncut[neighbor] - uncut[point]) / _run[way]);
}
