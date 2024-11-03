using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry.LSystems;
using RayTracer.Instructions.Core;

namespace RayTracer.Instructions.Surfaces.LSystems;

/// <summary>
/// This class is used to resolve an L-system value.
/// </summary>
public class LSystemResolver: SurfaceResolver<LSystem>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the axiom property of an L-system.
    /// </summary>
    public Resolver<string> AxiomResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the "generations" property of an L-system.
    /// </summary>
    public Resolver<int> GenerationsResolver { get; set; }

    /// <summary>
    /// This property holds the list of command mapping overrides to use.
    /// </summary>
    public List<LSystemRenderCommandMapping> CommandMappings { get; } = [];

    /// <summary>
    /// This property holds the resolver for the "generations" property of an L-system.
    /// </summary>
    public List<ProductionRuleResolver> ProductionRuleResolvers { get; set; } = [];

    /// <summary>
    /// This property holds the resolver for the rendering controls property of an L-system.
    /// </summary>
    public LSystemRenderingControlsResolver RenderingControlsResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// text solid surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, LSystem value)
    {
        AxiomResolver.AssignTo(value, target => target.Axiom, context, variables);
        GenerationsResolver.AssignTo(value, target => target.Generations, context, variables);
        RenderingControlsResolver.AssignTo(value, targter => targter.RenderingControls, context, variables);

        value.CommandMappings.AddRange(CommandMappings);
        value.ProductionRules.AddRange(ProductionRuleResolvers
            .Select(resolver => resolver.Resolve(context, variables)));

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (AxiomResolver is null)
            return "The \"axiom\" property is required.";

        return ProductionRuleResolvers.IsNullOrEmpty()
            ? "At least one production rule must be provided."
            : null;
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        LSystemResolver resolver = (LSystemResolver) base.Clone();

        if (resolver.RenderingControlsResolver is not null)
            resolver.RenderingControlsResolver = (LSystemRenderingControlsResolver) RenderingControlsResolver.Clone();

        return resolver;
    }
}
