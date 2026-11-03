using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a chain along a spline.
/// </summary>
public class ChainResolver : SurfaceResolver<Chain>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the spline the chain runs along.
    /// </summary>
    public Resolver<Spline> SplineResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how long a link is, end to end outside.
    /// </summary>
    public Resolver<double> LinkResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how thick a link's bar is.
    /// </summary>
    public Resolver<double> WireResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how wide a link is outside.
    /// </summary>
    public Resolver<double> WidthResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a chain.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Chain value)
    {
        if (SplineResolver is not null)
            value.Spline = SplineResolver.Resolve(context, variables);

        LinkResolver.AssignTo(value, target => target.Link, context, variables);

        if (WireResolver is not null)
            value.Wire = WireResolver.Resolve(context, variables);

        if (WidthResolver is not null)
            value.Width = WidthResolver.Resolve(context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method makes sure the chain says what it runs along.
    /// </summary>
    /// <returns>The text of an error message, or <c>null</c>, if all is well.</returns>
    public string Validate() => SplineResolver is null ? "A chain needs a spline to run along." : null;
}
