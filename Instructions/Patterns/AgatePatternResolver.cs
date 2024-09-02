using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve an agate pattern value.
/// </summary>
public class AgatePatternResolver : PatternResolver<AgatePattern>
{
    /// <summary>
    /// This property holds the resolver for our pigment's turbulence property.
    /// </summary>
    public Resolver<Turbulence> TurbulenceResolver { get; init; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an agate
    /// pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, AgatePattern value)
    {
        TurbulenceResolver.AssignTo(value, target => target.Turbulence, context, variables);
        
        base.SetProperties(context, variables, value);
    }
}
