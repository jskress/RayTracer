using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve an brick pattern value.
/// </summary>
public class BrickPatternResolver : PatternResolver<BrickPattern>
{
    /// <summary>
    /// This property holds the resolver for our pattern's brick size property.
    /// </summary>
    public Resolver<Vector> BrickSizeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our pattern's mortar size property.
    /// </summary>
    public Resolver<double> MortarSizeResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an agate
    /// pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, BrickPattern value)
    {
        BrickSizeResolver.AssignTo(value, target => target.BrickSize, context, variables);
        MortarSizeResolver.AssignTo(value, target => target.MortarSize, context, variables);

        base.SetProperties(context, variables, value);
    }
}
