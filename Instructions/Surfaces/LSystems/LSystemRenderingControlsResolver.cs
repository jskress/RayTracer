using RayTracer.General;
using RayTracer.Geometry.LSystems;
using RayTracer.Instructions.Core;

namespace RayTracer.Instructions.Surfaces.LSystems;

/// <summary>
/// This class is used to resolve an L-system rendering controls value.
/// </summary>
public class LSystemRenderingControlsResolver : ObjectResolver<LSystemRenderingControls>
{
    /// <summary>
    /// This property holds the resolver for the render type property of an L-system.
    /// </summary>
    public Resolver<LSystemRendererType> RenderTypeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the angle property of an L-system.
    /// </summary>
    public AngleResolver AngleResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the length property of an L-system.
    /// </summary>
    public Resolver<double> LengthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the diameter property of an L-system.
    /// </summary>
    public Resolver<double> DiameterResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the factor property of an L-system.
    /// </summary>
    public Resolver<double> FactorResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// text solid surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, LSystemRenderingControls value)
    {
        RenderTypeResolver.AssignTo(value, target => target.RendererType, context, variables);
        AngleResolver.AssignTo(value, target => target.Angle, context, variables);
        LengthResolver.AssignTo(value, target => target.Length, context, variables);
        DiameterResolver.AssignTo(value, target => target.Diameter, context, variables);
        FactorResolver.AssignTo(value, target => target.Factor, context, variables);
    }
}
