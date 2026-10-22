using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a tree grown by space colonization.
/// </summary>
public class GrownTreeResolver : SurfaceResolver<GrownTree>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the space the tree is to fill: a group written out in the
    /// block, a named surface or a call of a primitive.
    /// </summary>
    public ISurfaceResolver CrownResolver { get; set; }

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
        return CrownResolver is null
            ? "A grown tree needs a crown: the space it is to grow into."
            : null;
    }
}
