using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This interface defines the contract for something that can resolve to a pattern. 
/// </summary>
public interface IPatternResolver
{
    /// <summary>
    /// This method is used to execute the resolver to produce a value as a pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public Pattern ResolveToPattern(RenderContext context, Variables variables);
}
