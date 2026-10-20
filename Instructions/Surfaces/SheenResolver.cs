using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a material's sheen: the color its fibers give back, and how rough it is.
/// </summary>
public class SheenResolver : ObjectResolver<Sheen>
{
    /// <summary>
    /// This property holds the resolver for the color the sheen's fibers give back.
    /// </summary>
    public Resolver<Graphics.Color> ColorResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how rough the sheen is.
    /// </summary>
    public Resolver<double> RoughnessResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a sheen.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The sheen to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Sheen value)
    {
        ColorResolver.AssignTo(value, target => target.Color, context, variables);
        RoughnessResolver.AssignTo(value, target => target.Roughness, context, variables);
    }
}
