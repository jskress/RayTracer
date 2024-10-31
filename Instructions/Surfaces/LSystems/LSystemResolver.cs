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
    /// This property holds the resolver for the render type property of an L-system.
    /// </summary>
    public Resolver<LSystemRendererType> RenderTypeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the angle property of an L-system.
    /// </summary>
    public AngleResolver AngleResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the distance property of an L-system.
    /// </summary>
    public Resolver<double> DistanceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the diameter property of an L-system.
    /// </summary>
    public Resolver<double> DiameterResolver { get; set; }

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
        RenderTypeResolver.AssignTo(value, target => target.RendererType, context, variables);
        AngleResolver.AssignTo(value, target => target.Angle, context, variables);
        DistanceResolver.AssignTo(value, target => target.Distance, context, variables);
        DiameterResolver.AssignTo(value, target => target.Diameter, context, variables);

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
}
