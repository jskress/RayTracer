namespace RayTracer.Geometry;

/// <summary>
/// This class fills a grid of cells with tiles by Maxim Gumin's wave function collapse (2016), in its "simple tiled"
/// form: each tile names what lies on each of its sides, and two tiles may stand side by side only where the sides
/// they turn to each other say the same thing -- or, for labels paired to fit each other, say each other's.
/// <para>
/// **A pair makes adjacency directed.**  Sides saying the same thing are symmetric: a house's front labeled `curb`
/// to meet a road's `curb` would as gladly meet another house's front.  A label named in a pair (`fit 'curb' to
/// 'frontage'`) fits only what it is paired with, itself only if paired with itself, so a frontage meets a road's
/// curb and never another frontage.  Gumin's own tilesets say every neighbor outright; pairs are that, for the
/// labels that need it.
/// </para>
/// <para>
/// **Every cell starts able to hold any tile, in any of the turns it may be laid in** -- its states.  The outside
/// of the grid says what it presents, if anything, and the states that cannot face it are banned from the cells
/// along it.  Then, over and over, the cell with the least uncertainty left (Shannon's entropy over the weights of
/// the states it can still hold, a whisker of a hashed draw breaking ties) is collapsed to one state, drawn by
/// weight, and what that rules out next door, and next door to that, is banned in turn.  When every cell holds one
/// state the grid is laid; when a cell is left able to hold none, the attempt has painted itself into a corner and
/// it starts again with the next draws, as Gumin's does, up to a number of tries.
/// </para>
/// <para>
/// **Banning is by counting support**, Gumin's way, not by checking every pair again: each state at each cell keeps
/// how many states in its neighbor on each side still allow it, and banning a state takes one from each of the
/// counts it gave; a count reaching nought bans that state too.  So the work goes with the number of bans times the
/// states each allows, not with the square of the states at every look.
/// </para>
/// <para>
/// Every draw is hashed from the seed, the attempt and the cell (splitmix), never taken in order, so a grid laid
/// again comes out the same.
/// </para>
/// </summary>
internal sealed class WaveFunctionCollapse
{
    /// <summary>The sides, in order: right (+X), back (+Z), left (-X), front (-Z), top (+Y) and bottom (-Y).</summary>
    internal const int Right = 0, Back = 1, Left = 2, Front = 3, Top = 4, Bottom = 5;

    private static readonly (int X, int Y, int Z)[] Steps =
        [(1, 0, 0), (0, 0, 1), (-1, 0, 0), (0, 0, -1), (0, 1, 0), (0, -1, 0)];

    /// <summary>
    /// This record holds one state a cell may hold: a tile, laid in one of its turns, with what lies on each of its
    /// sides as laid (<c>null</c> for a side that fits anything), and how often it is drawn.
    /// </summary>
    internal readonly record struct State(int Tile, int Turn, string[] Sides, double Weight);

    private readonly IReadOnlyList<State> _states;
    private readonly HashSet<(string, string)> _fits;
    private readonly HashSet<string> _paired;
    private readonly int _across, _up, _along;
    private readonly int[][][] _allows;
    private readonly int _seed;
    private readonly int[] _supportAtFirst;

    private bool[] _possible;
    private int[] _support;
    private int[] _left;
    private double[] _weightSum, _weightLogSum;
    private Stack<(int Cell, int State)> _banned;

    /// <summary>
    /// This constructor sets up a grid of the given size to be filled with the given states.
    /// </summary>
    /// <param name="states">The states a cell may hold.</param>
    /// <param name="across">How many cells the grid has along X.</param>
    /// <param name="up">How many cells it has up Y.</param>
    /// <param name="along">How many cells it has along Z.</param>
    /// <param name="seed">The seed every draw is hashed from.</param>
    /// <param name="fits">The pairs of labels that fit each other, either way around; a label in a pair fits nothing
    /// else.</param>
    internal WaveFunctionCollapse(
        IReadOnlyList<State> states, int across, int up, int along, int seed,
        IEnumerable<(string, string)> fits = null)
    {
        _states = states;
        _fits = [];
        _paired = [];

        foreach ((string one, string other) in fits ?? [])
        {
            _fits.Add((one, other));
            _fits.Add((other, one));
            _paired.Add(one);
            _paired.Add(other);
        }

        _across = across;
        _up = up;
        _along = along;
        _seed = seed;

        // Which states may stand next door on each side of each state.
        _allows = new int[6][][];

        for (int side = 0; side < 6; side++)
        {
            _allows[side] = new int[states.Count][];

            for (int state = 0; state < states.Count; state++)
            {
                _allows[side][state] = Enumerable.Range(0, states.Count)
                    .Where(other => Matches(states[state].Sides[side], states[other].Sides[Opposite(side)]))
                    .ToArray();
            }
        }

        // How many states next door on each side of each state allow it -- those that allow it from their side --
        // the same in every cell before anything is banned.
        _supportAtFirst = new int[states.Count * 6];

        for (int side = 0; side < 6; side++)
        {
            foreach (int[] allowed in _allows[Opposite(side)])
            {
                foreach (int state in allowed)
                    _supportAtFirst[state * 6 + side]++;
            }
        }
    }

    /// <summary>
    /// This method notes whether two sides may face each other: when either says nothing; when either is paired, only
    /// as a pair; else when they say the same.
    /// </summary>
    internal bool Matches(string one, string other)
    {
        if (one is null || other is null)
            return true;

        return _paired.Contains(one) || _paired.Contains(other) ? _fits.Contains((one, other)) : one == other;
    }

    /// <summary>
    /// This method returns the side facing the given one across the line between two cells.
    /// </summary>
    internal static int Opposite(int side) => side < 4 ? (side + 2) % 4 : side == Top ? Bottom : Top;

    private int Cells => _across * _up * _along;

    private int CellAt(int x, int y, int z) => (y * _along + z) * _across + x;

    /// <summary>
    /// This method returns the cell next to a cell on one side, or -1 past the grid's edge.
    /// </summary>
    private int Beside(int cell, int side)
    {
        int x = cell % _across, z = cell / _across % _along, y = cell / (_across * _along);
        (int dx, int dy, int dz) = Steps[side];

        x += dx;
        y += dy;
        z += dz;

        return x < 0 || y < 0 || z < 0 || x >= _across || y >= _up || z >= _along ? -1 : CellAt(x, y, z);
    }

    /// <summary>
    /// This method lays the grid: the state each cell holds, cell by cell, X fastest, then Z, then Y.
    /// </summary>
    /// <param name="outside">What the outside of the grid presents on each side, or <c>null</c> for nothing.</param>
    /// <param name="tries">How many attempts to make before giving up.</param>
    /// <returns>The state of each cell, or <c>null</c> if no attempt could lay it.</returns>
    /// <exception cref="ArgumentException">When the outside and the tiles rule out every state of some cell before
    /// anything is drawn, which no draw can mend.</exception>
    internal int[] Lay(string[] outside, int tries)
    {
        for (int attempt = 0; attempt < tries; attempt++)
        {
            if (!Begin(outside))
            {
                throw new ArgumentException(
                    "The tiles cannot be laid at all: what lies outside the grid, and what the tiles allow beside " +
                    "one another, leave some cell no tile fits, whatever is drawn.");
            }

            if (Run(attempt))
                return Enumerable.Range(0, Cells).Select(HeldBy).ToArray();
        }

        return null;
    }

    /// <summary>
    /// This method returns the one state a laid cell holds.
    /// </summary>
    private int HeldBy(int cell) =>
        Array.IndexOf(_possible, true, cell * _states.Count, _states.Count) - cell * _states.Count;

    /// <summary>
    /// This method sets every cell able to hold every state, then bans what the outside and the tiles themselves rule
    /// out.
    /// </summary>
    /// <returns><c>false</c>, if that alone leaves some cell able to hold nothing.</returns>
    private bool Begin(string[] outside)
    {
        int count = _states.Count;

        _possible = new bool[Cells * count];
        _support = new int[Cells * count * 6];
        _left = new int[Cells];
        _weightSum = new double[Cells];
        _weightLogSum = new double[Cells];
        _banned = new Stack<(int, int)>();

        Array.Fill(_possible, true);

        double weights = _states.Sum(state => state.Weight);
        double weightLogs = _states.Sum(state => state.Weight * Math.Log(state.Weight));

        for (int cell = 0; cell < Cells; cell++)
        {
            _left[cell] = count;
            _weightSum[cell] = weights;
            _weightLogSum[cell] = weightLogs;
            Array.Copy(_supportAtFirst, 0, _support, cell * count * 6, count * 6);
        }

        for (int cell = 0; cell < Cells; cell++)
        {
            for (int side = 0; side < 6; side++)
            {
                bool edge = Beside(cell, side) < 0;

                for (int state = 0; state < count; state++)
                {
                    if (!_possible[cell * count + state])
                        continue;

                    // Past the edge, the outside must fit the side; inside, something next door must allow it.
                    bool ruledOut = edge
                        ? outside[side] is not null && !Matches(_states[state].Sides[side], outside[side])
                        : _support[(cell * count + state) * 6 + side] == 0;

                    if (ruledOut && !Ban(cell, state))
                        return false;
                }
            }
        }

        return Spread();
    }

    /// <summary>
    /// This method collapses cells one by one until every cell holds one state, or one holds none.
    /// </summary>
    /// <returns><c>true</c>, if the grid is laid.</returns>
    private bool Run(int attempt)
    {
        int count = _states.Count;

        while (true)
        {
            int chosen = -1;
            double least = double.MaxValue;

            for (int cell = 0; cell < Cells; cell++)
            {
                if (_left[cell] <= 1)
                    continue;

                double entropy = Math.Log(_weightSum[cell]) - _weightLogSum[cell] / _weightSum[cell] +
                                 1e-6 * Draw(attempt, cell, 0);

                if (entropy < least)
                {
                    least = entropy;
                    chosen = cell;
                }
            }

            if (chosen < 0)
                return true;

            // A state drawn by weight among those the cell can still hold, and every other banned.
            double pick = Draw(attempt, chosen, 1) * _weightSum[chosen];
            int keep = -1;

            for (int state = 0; state < count && keep < 0; state++)
            {
                if (!_possible[chosen * count + state])
                    continue;

                pick -= _states[state].Weight;

                if (pick <= 0)
                    keep = state;
            }

            if (keep < 0)
                keep = Array.LastIndexOf(_possible, true, chosen * count + count - 1, count) - chosen * count;

            for (int state = 0; state < count; state++)
            {
                if (state != keep && _possible[chosen * count + state] && !Ban(chosen, state))
                    return false;
            }

            if (!Spread())
                return false;
        }
    }

    /// <summary>
    /// This method bans a state at a cell, for what it allows next door to be taken back.
    /// </summary>
    /// <returns><c>false</c>, if the cell is left able to hold nothing.</returns>
    private bool Ban(int cell, int state)
    {
        int at = cell * _states.Count + state;
        double weight = _states[state].Weight;

        _possible[at] = false;
        _left[cell]--;
        _weightSum[cell] -= weight;
        _weightLogSum[cell] -= weight * Math.Log(weight);
        _banned.Push((cell, state));

        return _left[cell] > 0;
    }

    /// <summary>
    /// This method takes back what each banned state allowed next door, banning whatever that leaves unsupported.
    /// </summary>
    /// <returns><c>false</c>, if some cell is left able to hold nothing.</returns>
    private bool Spread()
    {
        int count = _states.Count;

        while (_banned.TryPop(out (int Cell, int State) banned))
        {
            for (int side = 0; side < 6; side++)
            {
                int next = Beside(banned.Cell, side);

                if (next < 0)
                    continue;

                foreach (int other in _allows[side][banned.State])
                {
                    int at = (next * count + other) * 6 + Opposite(side);

                    if (--_support[at] == 0 && _possible[next * count + other] && !Ban(next, other))
                        return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// This method draws a number from nought to one for a purpose at a cell in an attempt: the same every time.
    /// </summary>
    private double Draw(int attempt, int cell, int purpose)
    {
        ulong bits = (ulong) (uint) _seed * 0x9E3779B97F4A7C15UL ^ (ulong) (uint) attempt * 0xC2B2AE3D27D4EB4FUL ^
                     (ulong) (uint) cell * 0x165667B19E3779F9UL ^ (ulong) (uint) purpose * 0xD6E8FEB86659FD93UL;

        bits = (bits ^ (bits >> 30)) * 0xBF58476D1CE4E5B9UL;
        bits = (bits ^ (bits >> 27)) * 0x94D049BB133111EBUL;
        bits ^= bits >> 31;

        return (bits >> 11) * (1.0 / (1UL << 53));
    }
}
