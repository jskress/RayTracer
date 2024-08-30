using RayTracer.General;

namespace RayTracer.Instructions;

/// <summary>
/// This interface defines the contract for something that can resolve to an object. 
/// </summary>
public interface IObjectResolver
{
    /// <summary>
    /// This method is used to execute the resolver to produce a value as an object.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public object ResolveToObject(RenderContext context, Variables variables);
}
