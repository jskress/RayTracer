using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve a planks pattern value.
/// </summary>
public class PlanksPatternResolver : PatternResolver<PlanksPattern>
{
    /// <summary>
    /// This property holds the resolver for the size of a board.
    /// </summary>
    public Resolver<Vector> PlankSizeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how wide a seam is.
    /// </summary>
    public Resolver<double> SeamSizeResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a planks pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, PlanksPattern value)
    {
        PlankSizeResolver.AssignTo(value, target => target.PlankSize, context, variables);
        SeamSizeResolver.AssignTo(value, target => target.SeamSize, context, variables);

        if (value.PlankSize.X <= 0 || value.PlankSize.Y <= 0 || value.PlankSize.Z <= 0)
            throw new Exception("A plank must be some length, and some width both ways.");

        if (value.SeamSize < 0)
            throw new Exception("A seam cannot be narrower than nothing.");

        base.SetProperties(context, variables, value);
    }
}
