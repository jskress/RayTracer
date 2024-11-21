using System.Text;
using RayTracer.General;
using RayTracer.Geometry.LSystems;

namespace RayTracer.Instructions.Surfaces.LSystems;

/// <summary>
/// This class is used to resolve an L-system production rule value.
/// </summary>
public class ProductionRuleSpecResolver : ObjectResolver<ProductionRuleSpec>
{
    /// <summary>
    /// This property holds the resolver for the key property of the production rule.
    /// </summary>
    public Resolver<string> KeyResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the variable property of the production rule.
    /// </summary>
    public Resolver<Rune> VariableResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the left context property of the production rule.
    /// </summary>
    public Resolver<ProductionBranch> LeftContextResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the right context property of the production rule.
    /// </summary>
    public Resolver<ProductionBranch> RightContextResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the break value property of the production rule.
    /// </summary>
    public Resolver<double> BreakValueResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the production property of the production rule.
    /// </summary>
    public Resolver<string> ProductionResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// production rule.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, ProductionRuleSpec value)
    {
        KeyResolver.AssignTo(value, target => target.Key, context, variables);
        VariableResolver.AssignTo(value, target => target.Variable, context, variables);
        LeftContextResolver.AssignTo(value, target => target.LeftContext, context, variables);
        RightContextResolver.AssignTo(value, target => target.RightContext, context, variables);
        BreakValueResolver.AssignTo(value, target => target.BreakValue, context, variables);
        ProductionResolver.AssignTo(value, target => target.Production, context, variables);

        string error = value.Validate();

        if (error is not null)
            throw new Exception(error);
    }
}
