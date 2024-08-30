using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a turbulence value.
/// </summary>
public class TurbulenceResolver : ObjectResolver<Turbulence>
{
    /// <summary>
    /// This property holds the resolver for our turbulence's depth property.
    /// </summary>
    public Resolver<int> DepthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our turbulence's phased property.
    /// </summary>
    public Resolver<bool> PhasedResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our turbulence's tightness property.
    /// </summary>
    public Resolver<int> TightnessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our turbulence's scale property.
    /// </summary>
    public Resolver<double> ScaleResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a turbulence
    /// object.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Turbulence value)
    {
        DepthResolver.AssignTo(value, target => target.Depth, context, variables);
        PhasedResolver.AssignTo(value, target => target.Phased, context, variables);
        TightnessResolver.AssignTo(value, target => target.Tightness, context, variables);
        ScaleResolver.AssignTo(value, target => target.Scale, context, variables);
    }
}
