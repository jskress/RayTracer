using Regex = System.Text.RegularExpressions.Regex;
using RayTracer.Core;
using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;

namespace RayTracer.Renderer;

/// <summary>
/// This class counts what a scene holds: its surfaces, by kind, the combinations it makes of them, the
/// shapes it shares, and its lights, by kind.
/// <para>
/// **A surface is counted as the kind it was written as.**  A plain group is only a way of keeping
/// things together, so it is not counted, though everything in it is.  A kind that is built from
/// pieces of its own -- a height field from triangles, text from its glyphs, a tube from segments, an
/// L-system from whatever its turtle lays down, a sweep or a ribbon from whatever carries its shape --
/// counts once, as itself, since nobody wrote its pieces; how many pieces it took is kept alongside,
/// since that is what it costs to trace.  A field counts once as well, and its copies too, since they
/// are the scene's own surface set out many times over, just as a loop's would be.
/// </para>
/// <para>
/// **A combination counts as it was written.**  <c>union { a b c }</c> is one union, though it is
/// built as two.
/// </para>
/// <para>
/// **A shared shape counts once for every place it is shown.**  The surfaces behind an instance are
/// the instance's as much as anybody's, so a wood of a hundred copies of one tree counts a hundred
/// trees' worth.  Which shapes are shown more than once, and how many times, is counted as well --
/// only those, since every call of a primitive that makes a group is an instance, even one made a
/// single time, and those share nothing.
/// </para>
/// </summary>
public class SceneCensus
{
    /// <summary>
    /// This property holds how many surfaces there are of each kind.
    /// </summary>
    public IReadOnlyDictionary<string, long> Surfaces => _all.Surfaces;

    /// <summary>
    /// This property holds, for each kind built from pieces of its own, how many pieces there are of
    /// it altogether.
    /// </summary>
    public IReadOnlyDictionary<string, long> Pieces => _all.Pieces;

    /// <summary>
    /// This property holds how many unions the scene wrote.
    /// </summary>
    public long Unions => _all.Unions;

    /// <summary>
    /// This property holds how many intersections the scene wrote.
    /// </summary>
    public long Intersections => _all.Intersections;

    /// <summary>
    /// This property holds how many differences the scene wrote.
    /// </summary>
    public long Differences => _all.Differences;

    /// <summary>
    /// This property holds how many shapes the scene shows in more than one place.
    /// </summary>
    public int SharedShapes => _uses.Values.Count(uses => uses > 1);

    /// <summary>
    /// This property holds how many places, between them, those shapes are shown in.
    /// </summary>
    public long SharedUses => _uses.Values.Where(uses => uses > 1).Sum();

    /// <summary>
    /// This property holds how many lights there are of each kind.
    /// </summary>
    public IReadOnlyDictionary<string, long> Lights => _lights;

    // The kinds whose own name says better what they are than their class's does.
    private static readonly Dictionary<Type, string> Names = new ()
    {
        [typeof(BicubicPatch)] = "patch",
        [typeof(JuliaFractal)] = "julia",
        [typeof(LSystem)] = "L-system",
        [typeof(TextSolid)] = "text"
    };

    private readonly Tally _all = new ();
    private readonly Dictionary<Surface, Tally> _shared = [];
    private readonly Dictionary<Surface, long> _uses = [];
    private readonly Dictionary<string, long> _lights = [];

    /// <summary>
    /// This method counts what the given scene holds.
    /// </summary>
    /// <param name="scene">The scene to count.</param>
    /// <returns>The scene's census.</returns>
    public static SceneCensus Of(Scene scene)
    {
        SceneCensus census = new ();

        foreach (Surface surface in scene.Surfaces)
            census.Count(surface, census._all);

        foreach (Light light in scene.Lights)
        {
            string kind = NameOf(light.GetType());

            census._lights[kind] = census._lights.GetValueOrDefault(kind) + 1;
        }

        return census;
    }

    /// <summary>
    /// This method counts one surface, and whatever of it should be counted, into a tally.
    /// </summary>
    /// <param name="surface">The surface to count.</param>
    /// <param name="tally">The tally to count it into.</param>
    private void Count(Surface surface, Tally tally)
    {
        switch (surface)
        {
            case Instance instance:
                _uses[instance.Prototype] = _uses.GetValueOrDefault(instance.Prototype) + 1;
                tally.Add(SharedTally(instance.Prototype));
                break;
            case CsgSurface csgSurface:
                if (!csgSurface.IsChainLink)
                    tally.CountCombination(csgSurface.Operation);

                Count(csgSurface.Left, tally);
                Count(csgSurface.Right, tally);
                break;
            case HeightField or TextSolid or Sweep or Ribbon or Tube or LSystem:
                tally.CountSurface(NameOf(surface.GetType()), PiecesOf(surface));
                break;
            case Group group:
                if (group.GetType() != typeof(Group))
                    tally.CountSurface(NameOf(group.GetType()));

                foreach (Surface child in group.Surfaces)
                    Count(child, tally);
                break;
            default:
                tally.CountSurface(NameOf(surface.GetType()));
                break;
        }
    }

    /// <summary>
    /// This method counts the pieces a surface is built from: the surfaces in it that hold no others.
    /// </summary>
    /// <param name="surface">The surface to count the pieces of.</param>
    /// <returns>How many pieces it is built from.</returns>
    private static long PiecesOf(Surface surface)
    {
        return surface switch
        {
            Group group => group.Surfaces.Sum(PiecesOf),
            CsgSurface csgSurface => PiecesOf(csgSurface.Left) + PiecesOf(csgSurface.Right),
            Tube { Root: not null } tube => PiecesOf(tube.Root),
            Instance instance => PiecesOf(instance.Prototype),
            _ => 1
        };
    }

    /// <summary>
    /// This method gives the tally of a shared shape, counting it the first time it is asked for.
    /// </summary>
    /// <param name="shape">The shared shape.</param>
    /// <returns>Its tally.</returns>
    private Tally SharedTally(Surface shape)
    {
        if (!_shared.TryGetValue(shape, out Tally tally))
        {
            tally = new Tally();

            Count(shape, tally);

            _shared[shape] = tally;
        }

        return tally;
    }

    /// <summary>
    /// This method gives the name a kind is reported under: its class's name, in words, unless it
    /// has a better one of its own.  <c>HeightField</c> becomes "height field".
    /// </summary>
    /// <param name="type">The kind's class.</param>
    /// <returns>Its name.</returns>
    private static string NameOf(Type type)
    {
        return Names.TryGetValue(type, out string name)
            ? name
            : Regex.Replace(type.Name, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
    }

    /// <summary>
    /// This class holds the counts for some part of a scene.
    /// </summary>
    private class Tally
    {
        internal Dictionary<string, long> Surfaces { get; } = [];
        internal Dictionary<string, long> Pieces { get; } = [];
        internal long Unions { get; private set; }
        internal long Intersections { get; private set; }
        internal long Differences { get; private set; }

        internal void CountSurface(string kind, long pieces = 0)
        {
            Surfaces[kind] = Surfaces.GetValueOrDefault(kind) + 1;

            if (pieces > 0)
                Pieces[kind] = Pieces.GetValueOrDefault(kind) + pieces;
        }

        internal void CountCombination(CsgOperation operation)
        {
            switch (operation)
            {
                case CsgOperation.Union:
                    Unions++;
                    break;
                case CsgOperation.Intersection:
                    Intersections++;
                    break;
                case CsgOperation.Difference:
                    Differences++;
                    break;
            }
        }

        internal void Add(Tally other)
        {
            foreach ((string kind, long count) in other.Surfaces)
                Surfaces[kind] = Surfaces.GetValueOrDefault(kind) + count;

            foreach ((string kind, long count) in other.Pieces)
                Pieces[kind] = Pieces.GetValueOrDefault(kind) + count;

            Unions += other.Unions;
            Intersections += other.Intersections;
            Differences += other.Differences;
        }
    }
}
