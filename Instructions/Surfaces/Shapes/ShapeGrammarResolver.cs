using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces.Shapes;

/// <summary>
/// This class is used to resolve a shape grammar: the rules, the first of them to hand the first scope, and that
/// scope's size.
/// </summary>
public class ShapeGrammarResolver : SurfaceResolver<ShapeGrammar>, IValidatable
{
    /// <summary>
    /// This property holds the name of the rule the first scope is handed to.
    /// </summary>
    public string Axiom { get; set; }

    /// <summary>
    /// This property holds the resolver for the first scope's size: across X, up Y and back along Z, standing on
    /// the ground with its middle over the origin and its front toward -Z.
    /// </summary>
    public Resolver<Vector> SizeResolver { get; set; }

    /// <summary>
    /// This property holds the grammar's rules.
    /// </summary>
    public List<ShapeRule> Rules { get; set; } = [];

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a shape grammar, and then to
    /// carry the grammar out.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, ShapeGrammar value)
    {
        base.SetProperties(context, variables, value);

        // Only a resolver with rules builds anything: the block on a call of a primitive giving back a grammar is
        // read as another, holding a transform and no rules, and must not build a second building over the first.
        if (Rules.Count == 0 || Axiom is null || SizeResolver is null)
            return;

        Vector size = SizeResolver.Resolve(context, variables);
        ShapeScope first = new (
            new Point(-size.X / 2, 0, -size.Z / 2), new Vector(1, 0, 0), new Vector(0, 1, 0), new Vector(0, 0, 1),
            size.X, size.Y, size.Z);

        new ShapeDerivation(Rules, context, value.Seed ?? 0, surface => value.Add(surface))
            .Derive(Axiom, first, variables, 0);
    }

    /// <summary>
    /// This method creates a copy of this resolver, its list of rules its own.
    /// </summary>
    /// <returns>The copy.</returns>
    public override object Clone()
    {
        ShapeGrammarResolver resolver = (ShapeGrammarResolver) base.Clone();

        resolver.Rules = [..resolver.Rules];

        return resolver;
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (Axiom is null)
            return "A shape grammar needs an axiom: the rule its first scope is handed to.";

        if (SizeResolver is null)
            return "A shape grammar needs a size: how big its first scope is.";

        if (Rules.Count == 0)
            return "A shape grammar needs rules.";

        return Rules.Any(rule => rule.Name == Axiom) ? null : $"A shape grammar has no rule named '{Axiom}' to begin with.";
    }
}
