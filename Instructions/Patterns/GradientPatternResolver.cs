using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve a gradient pattern value.
/// </summary>
public class GradientPatternResolver : BandPatternResolver<GradientPattern>
{
    /// <summary>
    /// This property holds the resolver for our pigment's bouncing property.
    /// </summary>
    public Resolver<bool> BouncingResolver { get; init; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an agate
    /// pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, GradientPattern value)
    {
        BouncingResolver.AssignTo(value, target => target.Bouncing, context, variables);
        
        base.SetProperties(context, variables, value);
    }
}
