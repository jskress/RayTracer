using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a material's glints: how big its facets are, and for a surface that is not
/// rough, how far its grains lean.
/// </summary>
public class GlintsResolver : ObjectResolver<Glints>
{
    /// <summary>
    /// This property holds the resolver for how big a facet is, across.
    /// </summary>
    public Resolver<double> SizeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how rough the grains are, on a surface that is not rough.
    /// </summary>
    public Resolver<double> RoughnessResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of glints.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The glints to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Glints value)
    {
        SizeResolver.AssignTo(value, target => target.Size, context, variables);

        if (RoughnessResolver is not null)
            value.Roughness = RoughnessResolver.Resolve(context, variables);
    }
}
