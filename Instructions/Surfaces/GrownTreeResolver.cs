using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a tree grown by space colonization.
/// </summary>
public class GrownTreeResolver : SurfaceResolver<GrownTree>, IValidatable, ICoverResolver
{
    /// <summary>
    /// This property holds the resolver for the space the tree is to fill: a group written out in the
    /// block, a named surface or a call of a primitive.
    /// </summary>
    public ISurfaceResolver CrownResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the name of the surface the tree climbs, in place of a crown.
    /// </summary>
    public Resolver<string> AllOverResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the pattern saying where on the surface climbed points stand.
    /// </summary>
    public DensityShapeResolver PatchesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the patterned value below which no point stands.
    /// </summary>
    public Resolver<double> PatchesFromResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the patterned value at and above which a point always stands.
    /// </summary>
    public Resolver<double> PatchesToResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the way the surface climbed must face for a point to stand on it.
    /// </summary>
    public Resolver<Vector> FacingResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how strongly points favor that facing.
    /// </summary>
    public Resolver<double> FacingPowerResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the lowest a point on the surface climbed may be.
    /// </summary>
    public Resolver<double> AboveResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the highest a point on the surface climbed may be.
    /// </summary>
    public Resolver<double> BelowResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the steepest the surface climbed may be where a point stands.
    /// </summary>
    public Resolver<double> SteepestResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far apart a climber's points stand.
    /// </summary>
    public Resolver<double> SpacingResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the furthest the wood may run from where it starts.
    /// </summary>
    public Resolver<double> SpreadResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many points fill the crown.
    /// </summary>
    public Resolver<int> PointsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far a point reaches for a twig.
    /// </summary>
    public Resolver<double> ReachResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how near a branch comes before a point is used up.
    /// </summary>
    public Resolver<double> KillResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far a twig grows each step.
    /// </summary>
    public Resolver<double> StepResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the way growth is bent toward.
    /// </summary>
    public Resolver<Vector> TropismResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how readily growth bends toward the tropism.
    /// </summary>
    public Resolver<double> SusceptibilityResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how thick a twig's end is.
    /// </summary>
    public Resolver<double> TwigResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the pipe model's exponent: how branches add up where they join.
    /// </summary>
    public Resolver<double> JoiningResolver { get; set; }

    /// <summary>
    /// This property holds the resolvers for the surfaces to stand as leaves, if the tree has leaves: each place a
    /// leaf stands takes one of them at random.  Each is resolved once, as the tree grows, and shared where it may be;
    /// they are shared references rather than copies, as an L-system's leaf is.
    /// </summary>
    public List<ISurfaceResolver> LeafResolvers { get; set; } = [];

    /// <summary>
    /// This property holds the resolver for whether the leaves hang from the twigs rather than lie along them.
    /// </summary>
    public Resolver<bool> HangingResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a grown
    /// tree.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, GrownTree value)
    {
        PointsResolver.AssignTo(value, target => target.Points, context, variables);
        ReachResolver.AssignTo(value, target => target.Reach, context, variables);
        KillResolver.AssignTo(value, target => target.Kill, context, variables);
        StepResolver.AssignTo(value, target => target.Step, context, variables);
        TropismResolver.AssignTo(value, target => target.Tropism, context, variables);
        SusceptibilityResolver.AssignTo(value, target => target.Susceptibility, context, variables);
        TwigResolver.AssignTo(value, target => target.Twig, context, variables);
        JoiningResolver.AssignTo(value, target => target.Joining, context, variables);

        // Only a resolver that says something replaces what is there: the block on a call of a
        // primitive giving back a tree is read as another tree, holding a transform and no crown,
        // and must not wipe what the primitive just worked out.
        if (CrownResolver is not null)
            value.Crown = CrownResolver.ResolveToSurface(context, variables);

        if (LeafResolvers.Count > 0)
        {
            value.LeafFactories = LeafResolvers
                .Select(leaf => (Func<Surface>) (() => leaf.ResolveToSurface(context, variables)))
                .ToList();
        }

        HangingResolver.AssignTo(value, target => target.Hanging, context, variables);
        AllOverResolver.AssignTo(value, target => target.AllOver, context, variables);
        PatchesResolver.AssignTo(value, target => target.Patches, context, variables);
        PatchesFromResolver.AssignTo(value, target => target.PatchesFrom, context, variables);
        PatchesToResolver.AssignTo(value, target => target.PatchesTo, context, variables);
        FacingResolver.AssignTo(value, target => target.Facing, context, variables);
        FacingPowerResolver.AssignTo(value, target => target.FacingPower, context, variables);

        if (AboveResolver is not null)
            value.Above = AboveResolver.Resolve(context, variables);

        if (BelowResolver is not null)
            value.Below = BelowResolver.Resolve(context, variables);

        if (SteepestResolver is not null)
            value.Steepest = SteepestResolver.Resolve(context, variables);

        if (SpreadResolver is not null)
            value.Spread = SpreadResolver.Resolve(context, variables);

        if (SpacingResolver is not null)
            value.Spacing = SpacingResolver.Resolve(context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method creates a copy of this resolver, its list of leaves its own.
    /// </summary>
    /// <returns>The copy.</returns>
    public override object Clone()
    {
        GrownTreeResolver resolver = (GrownTreeResolver) base.Clone();

        resolver.LeafResolvers = [..resolver.LeafResolvers];

        return resolver;
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (CrownResolver is null && AllOverResolver is null)
            return "A grown tree needs a crown, the space it is to grow into, or a surface to climb all over.";

        return PointsResolver is not null && SpacingResolver is not null
            ? "A grown tree is given how many points it grows toward or how far apart they stand, not both."
            : null;
    }
}
