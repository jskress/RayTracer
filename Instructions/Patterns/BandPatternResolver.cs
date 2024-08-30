using RayTracer.General;
using RayTracer.Instructions.Core;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is the base class for things that resolve a banded pattern value.
/// </summary>
public abstract class BandPatternResolver<TValue> : PatternResolver<TValue>
    where TValue : BandPattern, new()
{
    /// <summary>
    /// This property holds the resolver for our pattern's band type property.
    /// </summary>
    public Resolver<BandType> BandTypeResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a camera.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TValue value)
    {
        BandTypeResolver.AssignTo(value, target => target.BandType, context, variables);
        
        base.SetProperties(context, variables, value);
    }
}
