using Lex.Parser;
using Lex.Tokens;
using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class holds one tile of a grid as a scene writes it: what stands in its cell (or nothing), what lies on
/// each of its sides, whether it may be laid in four turns, and how often it is drawn.
/// </summary>
public class TileSpec
{
    /// <summary>
    /// This property holds what stands in the tile's cell, or <c>null</c> for nothing.
    /// </summary>
    public ISurfaceResolver Surface { get; set; }

    /// <summary>
    /// This property holds the resolvers for what lies on each side, right, back, left, front, top and bottom; a
    /// side with none fits anything.
    /// </summary>
    public Resolver<string>[] Sides { get; } = new Resolver<string>[6];

    /// <summary>
    /// This property notes whether the tile may be laid in any of four turns about Y, or only as written.
    /// </summary>
    public bool Turns { get; set; }

    /// <summary>
    /// This property holds the resolver for how often the tile is drawn, against the others.
    /// </summary>
    public Resolver<double> WeightResolver { get; set; }
}

/// <summary>
/// This class resolves a cell's size, written as a vector or as one number for every side alike.
/// </summary>
public class CellSizeResolver : TermResolver<Vector>
{
    public CellSizeResolver()
    {
        PossibleTypes = [typeof(Vector), typeof(double)];
    }

    protected override Vector Coerce(RenderContext context, object value)
    {
        return value is Vector vector ? vector : new Vector((double) value, (double) value, (double) value);
    }
}

/// <summary>
/// This class is used to resolve a grid of tiles, and to lay it: the tiles are turned into states, the grid filled by
/// wave function collapse, and each tile built once and stood in every cell it was laid in.
/// </summary>
public class TileGridResolver : SurfaceResolver<TileGrid>
{
    /// <summary>
    /// This field holds how many tries the grid is given to be laid in, unless the scene says.
    /// </summary>
    public const int DefaultTries = 100;

    /// <summary>
    /// This property holds the resolver for how many cells the grid has across X, up Y and along Z.
    /// </summary>
    public Resolver<Vector> SizeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how big a cell is.
    /// </summary>
    public Resolver<Vector> CellResolver { get; set; }

    /// <summary>
    /// This property holds the resolvers for what the outside of the grid presents: on its four sides, above it and
    /// below it.
    /// </summary>
    public Resolver<string> EdgeResolver { get; set; }
    public Resolver<string> AboveResolver { get; set; }
    public Resolver<string> BelowResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many attempts the grid is given to be laid in.
    /// </summary>
    public Resolver<int> TriesResolver { get; set; }

    /// <summary>
    /// This property holds the resolvers for the pairs of labels that fit each other rather than themselves.
    /// </summary>
    public List<(Resolver<string> One, Resolver<string> Other)> Fits { get; set; } = [];

    /// <summary>
    /// This property holds the grid's tiles.
    /// </summary>
    public List<TileSpec> Tiles { get; set; } = [];

    /// <summary>
    /// This property holds the token a refusal points at: the word that began the grid.
    /// </summary>
    public Token ErrorToken { get; set; }

    /// <summary>
    /// This method applies our resolvers to a grid, and then lays it.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TileGrid value)
    {
        base.SetProperties(context, variables, value);

        // Only a resolver with tiles lays anything: the block on a call of a primitive giving back a grid holds only
        // what the call changes, and must not lay a second grid over the first.
        if (Tiles.Count == 0 || SizeResolver is null)
            return;

        Vector size = SizeResolver.Resolve(context, variables);
        (int across, int up, int along) = ((int) Math.Round(size.X), (int) Math.Round(size.Y), (int) Math.Round(size.Z));
        Vector cell = CellResolver?.Resolve(context, variables) ?? new Vector(1, 1, 1);

        if (across < 1 || up < 1 || along < 1)
            throw Refusal($"A grid of tiles must be at least one cell each way, not {across} x {up} x {along}.");

        List<WaveFunctionCollapse.State> states = StatesOf(context, variables);
        string edge = EdgeResolver?.Resolve(context, variables);
        string[] outside =
        [
            edge, edge, edge, edge, AboveResolver?.Resolve(context, variables), BelowResolver?.Resolve(context, variables)
        ];
        int tries = TriesResolver?.Resolve(context, variables) ?? DefaultTries;
        int[] laid;

        try
        {
            List<(string, string)> fits = Fits
                .Select(pair => (pair.One.Resolve(context, variables), pair.Other.Resolve(context, variables)))
                .ToList();

            laid = new WaveFunctionCollapse(states, across, up, along, value.Seed ?? 0, fits).Lay(outside, tries);
        }
        catch (ArgumentException exception)
        {
            throw Refusal(exception.Message);
        }

        if (laid is null)
        {
            throw Refusal(
                $"The tiles could not be laid in {tries} tries: some arrangement of sides leaves a cell no tile " +
                "fits.  A tile for it, or more tries, may mend it.");
        }

        Stand(laid, states, across, along, cell, context, variables, value);
    }

    /// <summary>
    /// This method turns the tiles into the states a cell may hold: each tile in each of its turns, its sides turned
    /// with it, and its weight shared among them, so that a tile laid four ways is drawn no more often for it.
    /// </summary>
    private List<WaveFunctionCollapse.State> StatesOf(RenderContext context, Variables variables)
    {
        List<WaveFunctionCollapse.State> states = [];

        for (int index = 0; index < Tiles.Count; index++)
        {
            TileSpec tile = Tiles[index];
            string[] sides = tile.Sides.Select(side => side?.Resolve(context, variables)).ToArray();
            double weight = tile.WeightResolver?.Resolve(context, variables) ?? 1;
            int turns = tile.Turns ? 4 : 1;

            if (weight <= 0)
                throw Refusal($"A tile's weight must be more than nought, not {weight}.");

            for (int turn = 0; turn < turns; turn++)
            {
                // Turned a quarter about Y, as `rotate Y 90` turns a thing, what faced right faces front, front left,
                // left back and back right: so the side now facing each way is the one a turn further around.
                string[] turned = new string[6];

                for (int side = 0; side < 4; side++)
                    turned[side] = sides[(side + turn) % 4];

                turned[WaveFunctionCollapse.Top] = sides[WaveFunctionCollapse.Top];
                turned[WaveFunctionCollapse.Bottom] = sides[WaveFunctionCollapse.Bottom];

                states.Add(new WaveFunctionCollapse.State(index, turn, turned, weight / turns));
            }
        }

        return states;
    }

    /// <summary>
    /// This method stands each tile in every cell it was laid in: the cell's middle on the cell's floor, the grid's
    /// middle over the origin, turned as it was laid.  Each is built once, and shared where it may be.
    /// </summary>
    private void Stand(
        int[] laid, List<WaveFunctionCollapse.State> states, int across, int along, Vector cell,
        RenderContext context, Variables variables, TileGrid value)
    {
        Surface[] built = new Surface[Tiles.Count];
        bool[] shared = new bool[Tiles.Count];
        bool[] used = new bool[Tiles.Count];

        for (int index = 0; index < laid.Length; index++)
        {
            WaveFunctionCollapse.State state = states[laid[index]];
            TileSpec tile = Tiles[state.Tile];

            if (tile.Surface is null)
                continue;

            if (built[state.Tile] is null)
            {
                built[state.Tile] = tile.Surface.ResolveToSurface(context, variables);
                shared[state.Tile] = Instance.MayBeShared(built[state.Tile]);
            }

            Surface copy;

            if (shared[state.Tile])
                copy = new Instance { Prototype = built[state.Tile] };
            else if (!used[state.Tile])
                copy = built[state.Tile];
            else
                copy = tile.Surface.ResolveToSurface(context, variables);

            used[state.Tile] = true;

            int x = index % across, z = index / across % along, y = index / (across * along);
            Matrix placed = Basics.Transforms.Translate(
                (x + 0.5 - across / 2.0) * cell.X, y * cell.Y, (z + 0.5 - along / 2.0) * cell.Z) *
                Basics.Transforms.RotateAroundY(90 * state.Turn);

            copy.Transform = placed * copy.Transform;
            value.Add(copy);
        }
    }

    /// <summary>
    /// This method makes a refusal pointing at the grid.
    /// </summary>
    private TokenException Refusal(string message) => new (message) { Token = ErrorToken };

    /// <summary>
    /// This method creates a copy of this resolver, its list of tiles its own.
    /// </summary>
    /// <returns>The copy.</returns>
    public override object Clone()
    {
        TileGridResolver resolver = (TileGridResolver) base.Clone();

        resolver.Tiles = [..resolver.Tiles];
        resolver.Fits = [..resolver.Fits];

        return resolver;
    }
}
