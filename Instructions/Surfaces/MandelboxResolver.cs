using Lex.Parser;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a Mandelbox.  It needs no <c>bounded by</c>: its factor and fold say how far it
/// reaches.
/// </summary>
public class MandelboxResolver : SurfaceResolver<Mandelbox>
{
    /// <summary>
    /// This property holds the resolver for how much each round magnifies the point.
    /// </summary>
    public Resolver<double> FactorResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the radius inside which the sphere fold magnifies by its greatest.
    /// </summary>
    public Resolver<double> InnerRadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the radius inside which the sphere fold turns a point inside out.
    /// </summary>
    public Resolver<double> OuterRadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far a coordinate may go before the box fold reflects it back.
    /// </summary>
    public Resolver<double> FoldResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many rounds are run.
    /// </summary>
    public Resolver<int> IterationsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the finest detail the surface shows.
    /// </summary>
    public Resolver<double> DetailResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how closely a crossing is pinned down.
    /// </summary>
    public Resolver<double> AccuracyResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the fractal.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Mandelbox value)
    {
        FactorResolver.AssignTo(value, target => target.Factor, context, variables);
        InnerRadiusResolver.AssignTo(value, target => target.InnerRadius, context, variables);
        OuterRadiusResolver.AssignTo(value, target => target.OuterRadius, context, variables);
        FoldResolver.AssignTo(value, target => target.Fold, context, variables);
        IterationsResolver.AssignTo(value, target => target.Iterations, context, variables);
        DetailResolver.AssignTo(value, target => target.Detail, context, variables);
        AccuracyResolver.AssignTo(value, target => target.Accuracy, context, variables);

        // The sphere fold turns inside out between its two radii, so the inner must be the lesser; each is checked
        // alone as it is resolved, the two together only here.
        if (value.InnerRadius >= value.OuterRadius)
        {
            throw new TokenException(
                $"A Mandelbox's inner radius ({value.InnerRadius}) must be less than its outer radius " +
                $"({value.OuterRadius}).")
            {
                Token = ((OuterRadiusResolver ?? InnerRadiusResolver) as TermResolver<double>)?.Term.ErrorToken
            };
        }

        base.SetProperties(context, variables, value);
    }
}
