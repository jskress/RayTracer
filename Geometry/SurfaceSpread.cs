using RayTracer.Basics;
using RayTracer.Core;

namespace RayTracer.Geometry;

/// <summary>
/// This class spreads places evenly over the whole of a surface, whatever its shape: as many as its area
/// holds at a spacing, none crowding another, each with the way out of the surface there.
/// <para>
/// **Places are found as hair finds its roots**, by casting straight lines through a sphere around the surface
/// from every direction (see <see cref="Hair.Grow"/>): lines like these cross any surface as often over one patch
/// as over any other of the same area, and how often they cross it measures the area.
/// </para>
/// <para>
/// **Crossings fall at random, and random places clump**: a few stand nearly on top of one another and gaps
/// open between them, which a grid of jittered places never shows.  So five times as many are found as are
/// wanted, and the most crowded are taken away one at a time until as many are left as the area holds -- Yuksel's
/// weighted sample elimination ("Sample Elimination for Generating Poisson Disk Sample Sets", Eurographics
/// 2015).  What is left lies as evenly as a jittered grid does, at exactly the number asked.
/// </para>
/// </summary>
internal static class SurfaceSpread
{
    /// <summary>
    /// The most places a surface may be covered with.
    /// </summary>
    internal const int Most = 500_000;

    // How many places are found for each one kept, and the fewest found however few are kept: the crossings
    // measure the area, and so how many are kept, to about one part in the root of their number.
    private const int Candidates = 5;
    private const int Fewest = 16_384;

    // How many lines are cast at a time, between looks at whether enough have been found.
    private const int Batch = 4096;

    // The most lines cast for each place wanted, before giving up on finding five of each.
    private const int MostLinesEach = 400;

    // Yuksel's weights: how near two places count as no nearer.
    private const double NearestShare = 0.65;
    private const double NearestPower = 1.5;

    // Two places whose surfaces face further apart than this are on different sides of something thin, and never
    // crowd each other: the top and bottom of a plank are each covered as fully as a thick slab's.
    private const double SameSide = -0.5;

    /// <summary>
    /// This record holds one place on the surface: where it is, the way out of the surface there, both in the
    /// space the places are wanted in, and its number among the places found, which is fixed by the surface and
    /// the seed alone.
    /// </summary>
    internal readonly record struct Spot(Point Point, Vector Normal, int Number);

    /// <summary>
    /// This method spreads places over a surface, as evenly as the spacing says.
    /// </summary>
    /// <param name="ground">The surface to cover, readied for rendering.</param>
    /// <param name="space">The transform carrying the space the places are wanted in into the one the surface is
    /// placed in; the spacing is a distance in the space the places are wanted in.</param>
    /// <param name="spacing">How far apart the places stand: the area holds one for every square of this side.</param>
    /// <param name="seed">The seed the lines are cast from.</param>
    /// <param name="name">What the surface is called, for what is said if it cannot be covered.</param>
    /// <returns>The places, in the order they were found.</returns>
    internal static List<Spot> Over(Surface ground, Matrix space, double spacing, int seed, string name)
    {
        return Over(
            ground, space, spacing, seed,
            wanted => $"A field all over '{name}' would stand {wanted:N0} copies, and may stand at most {Most:N0}; " +
                      "give it a wider spacing.",
            $"A field may cover only a surface with an end to it all over, and '{name}' goes on forever; " +
            "drop the field onto it with `over` instead.");
    }

    /// <summary>
    /// This method spreads places over a surface, as evenly as the spacing says, saying what it is told to of a
    /// surface that cannot be covered so.
    /// </summary>
    /// <param name="ground">The surface to cover, readied for rendering.</param>
    /// <param name="space">The transform carrying the space the places are wanted in into the one the surface is
    /// placed in.</param>
    /// <param name="spacing">How far apart the places stand.</param>
    /// <param name="seed">The seed the lines are cast from.</param>
    /// <param name="tooMany">What to say of more places wanted than there may be.</param>
    /// <param name="endless">What to say of a surface that goes on forever.</param>
    /// <returns>The places, in the order they were found.</returns>
    internal static List<Spot> Over(
        Surface ground, Matrix space, double spacing, int seed, Func<long, string> tooMany, string endless)
    {
        return Spread(ground, space, area => (long) Math.Round(area / (spacing * spacing)), seed, tooMany, endless);
    }

    /// <summary>
    /// This method spreads a number of places over a surface, as evenly as so many may lie.
    /// </summary>
    /// <param name="ground">The surface to cover, readied for rendering.</param>
    /// <param name="space">The transform carrying the space the places are wanted in into the one the surface is
    /// placed in.</param>
    /// <param name="count">How many places to spread.</param>
    /// <param name="seed">The seed the lines are cast from.</param>
    /// <param name="endless">What to say of a surface that goes on forever.</param>
    /// <returns>The places, in the order they were found.</returns>
    internal static List<Spot> Over(Surface ground, Matrix space, int count, int seed, string endless)
    {
        return Spread(ground, space, _ => Math.Min(count, Most), seed, null, endless);
    }

    /// <summary>
    /// This method spreads places over a surface, as many as are wanted for the area its lines measure.
    /// </summary>
    /// <param name="ground">The surface to cover, readied for rendering.</param>
    /// <param name="space">The transform carrying the space the places are wanted in into the one the surface is
    /// placed in.</param>
    /// <param name="wantedFor">How many places are wanted, for the area measured so far.</param>
    /// <param name="seed">The seed the lines are cast from.</param>
    /// <param name="tooMany">What to say of a number wanted past the most there may be, or <c>null</c> if the
    /// number wanted never is.</param>
    /// <param name="endless">What to say of a surface that goes on forever.</param>
    /// <returns>The places, in the order they were found.</returns>
    private static List<Spot> Spread(
        Surface ground, Matrix space, Func<double, long> wantedFor, int seed, Func<long, string> tooMany,
        string endless)
    {
        BoundingBox shared = Surface.BoxAround(ground, true);

        // Something with nothing in it -- a snow cap out of season -- is covered with nothing.
        if (shared is { IsEmpty: true })
            return [];

        if (shared is null || !double.IsFinite((shared.Maximum - shared.Minimum).Magnitude))
            throw new Exception(endless);

        Matrix own = space.Invert();
        Matrix inward = ground.Transform.Invert() * space;
        Matrix outward = inward.Transpose();
        (Point center, double radius) = BallAround(shared, own);
        Random random = new (seed);
        List<Spot> found = [];
        List<Intersection> hits = [];
        long lines = 0;
        long wanted;

        while (true)
        {
            for (int index = 0; index < Batch; index++)
                CastLine(ground, inward, outward, center, radius, random, found, hits);

            lines += Batch;
            wanted = wantedFor(AreaOf(found.Count, lines, radius));

            if (wanted > Most)
                throw new Exception(tooMany(wanted));

            if (found.Count >= Math.Max(Candidates * wanted, Fewest) || (found.Count == 0 && lines >= Batch * 16) ||
                lines >= MostLinesEach * Math.Max(wanted, Batch))
                break;
        }

        return wanted == 0 ? [] : Thinned(found, (int) wanted, AreaOf(found.Count, lines, radius));
    }

    /// <summary>
    /// This method keeps the places spread over a surface that stand within what the thing covering it allows: its
    /// heights, its steepest, and the way the surface must face -- the last by chance, drawn from the place's number.
    /// Patches are asked apart, since a field keeps any place by them, wherever it was found.
    /// </summary>
    /// <param name="cover">The thing covering the surface.</param>
    /// <param name="spots">The places spread over it, in its own space.</param>
    /// <param name="seed">The seed the draws are made from.</param>
    /// <returns>The places kept, in the order they came.</returns>
    internal static IEnumerable<Spot> KeptBy(ICoversASurface cover, IEnumerable<Spot> spots, int seed)
    {
        Matrix outward = cover.Transform.Invert().Transpose();
        Vector facing = cover.Facing?.Unit;

        foreach (Spot spot in spots)
        {
            Point shared = cover.Transform * spot.Point;
            Vector up = (outward * spot.Normal).Unit;

            if (shared.Y < cover.Above || shared.Y > cover.Below)
                continue;

            if (cover.Steepest is { } steepest && Math.Acos(Math.Clamp(up.Y, -1, 1)) > steepest + 1e-12)
                continue;

            if (facing is not null &&
                Field.DrawFor(seed, spot.Number, 0, Field.FacingDraw) >=
                Math.Pow(Math.Max(0, up.Dot(facing)), cover.FacingPower))
                continue;

            yield return spot;
        }
    }

    /// <summary>
    /// This method reports whether the patches of a thing covering a surface, or of any field, keep a place.
    /// <para>
    /// **An edge turned about keeps exactly the places the edge the right way round leaves**, not merely
    /// as many: it keeps a place where the draw is at or above the chance the other would have given,
    /// rather than below its own.  So a meadow given the same pattern as a wood, with its edge reversed,
    /// fills the wood's clearings place for place, wherever the two share a grid.
    /// </para>
    /// </summary>
    /// <param name="cover">The thing whose patches they are.</param>
    /// <param name="at">The place, in the thing's own space.</param>
    /// <param name="row">The place's row, or its number among the places found over a surface.</param>
    /// <param name="column">The place's column, or nought for a place found over a surface.</param>
    /// <param name="seed">The seed the draws are made from.</param>
    /// <returns>Whether the place is kept.</returns>
    internal static bool KeptByThePatches(ICoversASurface cover, Point at, int row, int column, int seed)
    {
        double draw = Field.DrawFor(seed, row, column, Field.PatchDraw);
        double chance = ChanceAt(cover, at);

        return cover.PatchesFrom > cover.PatchesTo ? 1 - draw <= chance : draw < chance;
    }

    /// <summary>
    /// This method returns the chance a place is kept, by the patches.
    /// </summary>
    private static double ChanceAt(ICoversASurface cover, Point at)
    {
        double value = cover.Patches.ValueAt(at);

        // An edge with no width is a line: everything on one side of it, nothing on the other.
        if (cover.PatchesFrom == cover.PatchesTo)
            return value >= cover.PatchesFrom ? 1 : 0;

        return Math.Clamp((value - cover.PatchesFrom) / (cover.PatchesTo - cover.PatchesFrom), 0, 1);
    }

    /// <summary>
    /// This method returns a surface's area, measured by the lines cast at it: twice the area of the sphere's great
    /// circle for each crossing per line.
    /// </summary>
    private static double AreaOf(long crossings, long lines, double radius) =>
        2 * Math.PI * radius * radius * crossings / lines;

    /// <summary>
    /// This method returns a ball holding the surface, in the space the places are wanted in, from its box in the
    /// space it is placed in.
    /// </summary>
    private static (Point Center, double Radius) BallAround(BoundingBox box, Matrix own)
    {
        List<Point> corners = [];

        foreach (double x in (double[]) [box.Minimum.X, box.Maximum.X])
        foreach (double y in (double[]) [box.Minimum.Y, box.Maximum.Y])
        foreach (double z in (double[]) [box.Minimum.Z, box.Maximum.Z])
            corners.Add(own * new Point(x, y, z));

        Point center = new (
            corners.Average(point => point.X), corners.Average(point => point.Y), corners.Average(point => point.Z));
        double radius = corners.Max(point => (point - center).Magnitude);

        return (center, radius * 1.001 + 1e-9);
    }

    /// <summary>
    /// This method casts one line at the surface, from a random direction through a random point of the ball's
    /// cross-section, and keeps every crossing as a place.  It is hair's way of casting one (see
    /// <see cref="Hair.Grow"/>), asking the surface directly as it stands still.
    /// </summary>
    private static void CastLine(
        Surface ground, Matrix inward, Matrix outward, Point center, double radius, Random random, List<Spot> found,
        List<Intersection> hits)
    {
        Vector direction = Hair.RandomUnit(random);
        (Vector across, Vector up) = Microfacets.FrameAround(direction);
        double reach = radius * Math.Sqrt(random.NextDouble());
        double turn = 2 * Math.PI * random.NextDouble();
        Point start = center + across * (reach * Math.Cos(turn)) + up * (reach * Math.Sin(turn)) -
                      direction * (2 * radius);
        Ray line = new (start, direction);
        Ray local = inward.Transform(line);

        hits.Clear();
        ground.AddIntersections(local, hits);

        foreach (Intersection hit in hits)
        {
            // Copies stand on the skin, not on any hair it grows.
            if (hit.Surface is StrandSurface || Hair.NormalWithin(ground, hit, local.At(hit.Distance)) is not { } normal)
                continue;

            found.Add(new Spot(line.At(hit.Distance), (outward * normal).Unit, found.Count));
        }
    }

    /// <summary>
    /// This method takes the most crowded places away, one at a time, until as many are left as are wanted.
    /// <para>
    /// A place's crowding is the sum, over every other place within twice the largest spacing places so many
    /// could have, of how near it is -- one less its distance over that reach, to the eighth -- and taking a place
    /// away takes its share from each of its neighbors'.  Nearer than a share of the reach, a neighbor crowds
    /// no more, so that a pair found nearly on top of each other does not outweigh everything else.
    /// </para>
    /// </summary>
    /// <param name="found">Every place found.</param>
    /// <param name="wanted">How many to keep.</param>
    /// <param name="area">The surface's area.</param>
    /// <returns>The places kept, in the order they were found.</returns>
    private static List<Spot> Thinned(List<Spot> found, int wanted, double area)
    {
        if (found.Count <= wanted)
            return found;

        // The largest spacing as many places could have, packed as tightly as places on a surface can be.
        double reach = 2 * Math.Sqrt(area / (2 * Math.Sqrt(3) * wanted));
        double nearest = reach * NearestShare * (1 - Math.Pow((double) wanted / found.Count, NearestPower));
        Crowd crowd = new (found, reach, nearest);
        Heaviest heap = new (crowd.Crowding);

        for (int left = found.Count; left > wanted; left--)
            crowd.TakeAway(heap.Pop(), heap);

        return crowd.Kept();
    }

    /// <summary>
    /// This class holds the places being thinned, sorted by the cell of a grid each lies in so that neighbors lie
    /// together, with each cell's neighboring cells listed once, and how crowded each place is.
    /// </summary>
    private sealed class Crowd
    {
        private readonly List<Spot> _found;
        private readonly int[] _original;
        private readonly double[] _x, _y, _z, _nx, _ny, _nz;
        private readonly int[] _cellOf;
        private readonly int[] _cellStart;
        private readonly int[][] _cellsNear;
        private readonly bool[] _gone;
        private readonly double _reach;
        private readonly double _nearest;

        /// <summary>
        /// This property holds how crowded each place is, in the order they are sorted in.
        /// </summary>
        internal double[] Crowding { get; }

        internal Crowd(List<Spot> found, double reach, double nearest)
        {
            int count = found.Count;
            int[] cellOfFound = new int[count];
            Dictionary<(int, int, int), int> cells = [];

            _found = found;
            _reach = reach;
            _nearest = nearest;

            for (int index = 0; index < count; index++)
            {
                Point point = found[index].Point;
                (int, int, int) key = (
                    (int) Math.Floor(point.X / reach), (int) Math.Floor(point.Y / reach), (int) Math.Floor(point.Z / reach));

                if (!cells.TryGetValue(key, out int cell))
                    cells[key] = cell = cells.Count;

                cellOfFound[index] = cell;
            }

            _original = Enumerable.Range(0, count).OrderBy(index => cellOfFound[index]).ThenBy(index => index).ToArray();
            _x = new double[count];
            _y = new double[count];
            _z = new double[count];
            _nx = new double[count];
            _ny = new double[count];
            _nz = new double[count];
            _cellOf = new int[count];
            _cellStart = new int[cells.Count + 1];
            _gone = new bool[count];
            Crowding = new double[count];

            for (int index = 0; index < count; index++)
            {
                Spot spot = found[_original[index]];

                (_x[index], _y[index], _z[index]) = (spot.Point.X, spot.Point.Y, spot.Point.Z);
                (_nx[index], _ny[index], _nz[index]) = (spot.Normal.X, spot.Normal.Y, spot.Normal.Z);
                _cellOf[index] = cellOfFound[_original[index]];
                _cellStart[_cellOf[index] + 1]++;
            }

            for (int cell = 0; cell < cells.Count; cell++)
                _cellStart[cell + 1] += _cellStart[cell];

            _cellsNear = new int[cells.Count][];

            foreach (((int x, int y, int z), int cell) in cells)
            {
                List<int> near = [];

                for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (cells.TryGetValue((x + dx, y + dy, z + dz), out int other))
                        near.Add(other);
                }

                _cellsNear[cell] = near.ToArray();
            }

            for (int index = 0; index < count; index++)
                Crowding[index] = Visit(index, null);
        }

        /// <summary>
        /// This method takes a place away, taking its share of the crowding from each of its neighbors.
        /// </summary>
        internal void TakeAway(int index, Heaviest heap)
        {
            _gone[index] = true;

            Visit(index, heap);
        }

        /// <summary>
        /// This method returns the places left, in the order they were found.
        /// </summary>
        internal List<Spot> Kept()
        {
            List<int> kept = [];

            for (int index = 0; index < _gone.Length; index++)
            {
                if (!_gone[index])
                    kept.Add(_original[index]);
            }

            kept.Sort();

            return kept.Select(index => _found[index]).ToList();
        }

        /// <summary>
        /// This method sums how much a place's neighbors crowd it -- or, handed the order a place is being taken
        /// out of, takes its share from each of them instead.
        /// </summary>
        private double Visit(int index, Heaviest heap)
        {
            double total = 0;
            double x = _x[index], y = _y[index], z = _z[index];
            double nx = _nx[index], ny = _ny[index], nz = _nz[index];
            double reachSquared = _reach * _reach;

            foreach (int cell in _cellsNear[_cellOf[index]])
            {
                for (int other = _cellStart[cell]; other < _cellStart[cell + 1]; other++)
                {
                    if (other == index || _gone[other] || _nx[other] * nx + _ny[other] * ny + _nz[other] * nz < SameSide)
                        continue;

                    double dx = _x[other] - x, dy = _y[other] - y, dz = _z[other] - z;
                    double squared = dx * dx + dy * dy + dz * dz;

                    if (squared >= reachSquared)
                        continue;

                    // One less the distance over the reach, to the eighth.
                    double share = 1 - Math.Max(Math.Sqrt(squared), _nearest) / _reach;

                    share *= share;
                    share *= share;
                    share *= share;

                    if (heap is null)
                        total += share;
                    else
                    {
                        Crowding[other] -= share;
                        heap.Lightened(other);
                    }
                }
            }

            return total;
        }
    }

    /// <summary>
    /// This class keeps places in order of how crowded they are, the most crowded first, as their crowding falls.
    /// Two alike are taken in the order they were found, so the same surface and seed always keep the same places.
    /// </summary>
    private sealed class Heaviest
    {
        private readonly double[] _weights;
        private readonly int[] _heap;
        private readonly int[] _at;
        private int _count;

        internal Heaviest(double[] weights)
        {
            _weights = weights;
            _heap = new int[weights.Length];
            _at = new int[weights.Length];
            _count = weights.Length;

            for (int index = 0; index < _count; index++)
                _heap[index] = _at[index] = index;

            for (int index = _count / 2 - 1; index >= 0; index--)
                Down(index);
        }

        /// <summary>
        /// This method takes the most crowded place out and returns it.
        /// </summary>
        internal int Pop()
        {
            int top = _heap[0];

            _count--;
            Swap(0, _count);
            _at[top] = -1;

            if (_count > 0)
                Down(0);

            return top;
        }

        /// <summary>
        /// This method moves a place down the order once its crowding has fallen.
        /// </summary>
        internal void Lightened(int index)
        {
            if (_at[index] >= 0)
                Down(_at[index]);
        }

        private bool Over(int a, int b) =>
            _weights[a] > _weights[b] || (_weights[a] == _weights[b] && a < b);

        private void Down(int position)
        {
            while (true)
            {
                int left = 2 * position + 1, right = left + 1, top = position;

                if (left < _count && Over(_heap[left], _heap[top]))
                    top = left;

                if (right < _count && Over(_heap[right], _heap[top]))
                    top = right;

                if (top == position)
                    return;

                Swap(position, top);
                position = top;
            }
        }

        private void Swap(int a, int b)
        {
            (_heap[a], _heap[b]) = (_heap[b], _heap[a]);
            _at[_heap[a]] = a;
            _at[_heap[b]] = b;
        }
    }
}
