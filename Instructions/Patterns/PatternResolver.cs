using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is the base class for all pattern resolvers and is used to resolve a pattern
/// value.
/// </summary>
public abstract class PatternResolver<TValue> : ObjectResolver<TValue>, IPatternResolver
    where TValue : Pattern, new()
{
    /// <summary>
    /// This method is used to execute the resolver to produce a value as a pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public Pattern ResolveToPattern(RenderContext context, Variables variables)
    {
        return Resolve(context, variables);
    }

    /// <summary>
    /// This property holds the resolver for the turbulence to stir this pattern's points with.
    /// </summary>
    public Resolver<Turbulence> TurbulenceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for what this pattern's value is multiplied by before it
    /// is shaped.
    /// </summary>
    public Resolver<double> FrequencyResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for what is added to this pattern's value before shaping.
    /// </summary>
    public Resolver<double> PhaseResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how this pattern's value is shaped once produced.
    /// </summary>
    public Resolver<WaveType> WaveResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the exponent a polynomial wave uses.
    /// </summary>
    public Resolver<double> ExponentResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a pattern.
    /// Even though we don't have any of our own, we override this to provide an empty
    /// implementation since most patters don't have their own properties.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TValue value)
    {
        TurbulenceResolver.AssignTo(value, target => target.Turbulence, context, variables);
        FrequencyResolver.AssignTo(value, target => target.Frequency, context, variables);
        PhaseResolver.AssignTo(value, target => target.Phase, context, variables);
        WaveResolver.AssignTo(value, target => target.Wave, context, variables);
        ExponentResolver.AssignTo(value, target => target.Exponent, context, variables);

    }
}
