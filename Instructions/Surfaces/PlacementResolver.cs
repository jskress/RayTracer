using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class resolves one thing a scene said about where a surface goes in terms of another.
/// <para>
/// The relation itself is a word, settled while the scene was read, but the name it refers to is an
/// expression like any other -- so a loop may place a run of things against a run of names it works
/// out as it goes, and a library may take the name to place against as an argument.
/// </para>
/// </summary>
public class PlacementResolver
{
    /// <summary>
    /// This property holds which way round the two surfaces go.
    /// </summary>
    public PlacementRelation Relation { get; init; }

    /// <summary>
    /// This property holds the resolver for the name of the surface to place against.
    /// </summary>
    public Resolver<string> NameResolver { get; init; }

    /// <summary>
    /// This property holds the resolver for how far past the named place to go, or <c>null</c>.
    /// </summary>
    public Resolver<double> OffsetResolver { get; init; }

    /// <summary>
    /// This method works out the placement this stands for.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The placement, with the name it refers to settled.</returns>
    public Placement Resolve(RenderContext context, Variables variables)
    {
        return new Placement
        {
            Relation = Relation,
            TargetName = NameResolver.Resolve(context, variables),
            Offset = OffsetResolver?.Resolve(context, variables) ?? 0
        };
    }
}
