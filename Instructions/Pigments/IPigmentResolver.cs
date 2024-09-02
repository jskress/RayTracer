using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This interface defines the contract for something that can resolve to a pigment. 
/// </summary>
public interface IPigmentResolver : IObjectResolver
{
    /// <summary>
    /// This method is used to execute the resolver to produce a value as a pigment.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public Pigment ResolveToPigment(RenderContext context, Variables variables);
}
