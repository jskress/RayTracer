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
    /// This method is used to apply our resolvers to the appropriate properties of a pattern.
    /// Even though we don't have any of our own, we override this to provide an empty
    /// implementation since most patters don't have their own properties.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TValue value)
    {
    }
}
