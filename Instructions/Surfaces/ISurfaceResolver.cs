using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This interface defines the contract for something that can resolve to a surface. 
/// </summary>
public interface ISurfaceResolver : IObjectResolver, ICloneable
{
    /// <summary>
    /// This method is used to execute the resolver to produce a value as a surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public Surface ResolveToSurface(RenderContext context, Variables variables);
}
