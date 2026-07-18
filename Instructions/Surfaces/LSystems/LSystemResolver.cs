using System.Text;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Geometry.LSystems;

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
    public List<LSystemRenderCommandMapping> CommandMappings { get; private set; } = [];

    /// <summary>
    /// This property holds the resolver for the "generations" property of an L-system.
    /// </summary>
    public List<ProductionRuleSpecResolver> ProductionRuleResolvers { get; set; } = [];

    /// <summary>
    /// This property holds the resolver for the rendering controls property of an L-system.
    /// </summary>
    public LSystemRenderingControlsResolver RenderingControlsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for whether turtle orientation commands should be
    /// ignored.
    /// </summary>
    public Resolver<bool> IgnoreOrientationCommandsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for any extra symbols that should be ignored.
    /// </summary>
    public Resolver<Rune[]> SymbolsToIgnoreResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the surface to use as this L-system's leaf, when
    /// the scene names one.  When it is left <c>null</c>, the L-system falls back to its built-in
    /// default leaf.  It is a shared reference to a named surface's resolver, resolved afresh for
    /// each leaf, so it is not deep-copied on <see cref="Clone"/>.
    /// </summary>
    public ISurfaceResolver LeafSurfaceResolver { get; set; }

    /// <summary>
    /// This property holds the surfaces this L-system's production may name after a <c>~</c>,
    /// each tied to the character that names it.  These are the <c>~L</c>/<c>~K</c> bindings; a
    /// <c>~</c> naming none of them falls back to <see cref="LeafSurfaceResolver"/>.
    /// </summary>
    public List<LSystemSurfaceBinding> SurfaceBindings { get; private set; } = [];

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
        RenderingControlsResolver.AssignTo(value, target => target.RenderingControls, context, variables);
        IgnoreOrientationCommandsResolver.AssignTo(value, target => target.IgnoreOrientationCommands, context, variables);
        SymbolsToIgnoreResolver.AssignTo(value, target => target.SymbolsToIgnore, context, variables);

        value.CommandMappings.AddRange(CommandMappings);
        value.ProductionRules.AddRange(ProductionRuleResolvers
            .Select(resolver => resolver.Resolve(context, variables)));

        // Capture the leaf recipe here, where the render context and variables are in hand: the
        // L-system itself has neither at render time, when it stamps each leaf down.  A named
        // leaf surface is resolved fresh per leaf; with no name, the built-in default is used.
        value.LeafFactory = LeafSurfaceResolver is null
            ? DefaultLeaf.Create
            : () => LeafSurfaceResolver.ResolveToSurface(context, variables);

        foreach (LSystemSurfaceBinding binding in SurfaceBindings)
        {
            ISurfaceResolver resolver = binding.Resolver;

            value.SurfaceFactories[binding.Character] =
                () => resolver.ResolveToSurface(context, variables);
        }

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

        // Force the lists to be physically different, but with the same content.
        resolver.CommandMappings = [..resolver.CommandMappings];
        resolver.SurfaceBindings = [..resolver.SurfaceBindings];

        if (resolver.RenderingControlsResolver is not null)
            resolver.RenderingControlsResolver = (LSystemRenderingControlsResolver) RenderingControlsResolver.Clone();

        return resolver;
    }
}
