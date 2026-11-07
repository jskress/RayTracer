using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve how a pattern's space is bent: the functions, of the point, that each coordinate
/// the pattern reads is made of, any left unsaid standing as they are.
/// </summary>
public class WarpResolver : Resolver<Warp>
{
    /// <summary>
    /// This property holds the resolver for the function the bent X is, if one was said.
    /// </summary>
    public Resolver<FieldExpression> XResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the function the bent Y is, if one was said.
    /// </summary>
    public Resolver<FieldExpression> YResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the function the bent Z is, if one was said.
    /// </summary>
    public Resolver<FieldExpression> ZResolver { get; set; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override Warp Resolve(RenderContext context, Variables variables)
    {
        return new Warp(
            XResolver?.Resolve(context, variables),
            YResolver?.Resolve(context, variables),
            ZResolver?.Resolve(context, variables));
    }
}
