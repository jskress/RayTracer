using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This interface defines the contract for something that can resolve to a surface. 
/// </summary>
public interface ISurfaceResolver : IObjectResolver, ICloneable
{
    /// <summary>
    /// This property notes whether this resolver would make the surface give light.
    /// <para>
    /// It exists so that a call of a primitive can tell whether it may share the shape its body makes.  A light made
    /// of the glowing stuff inside a thing is made once for each surface that glows, not once for each place a shared
    /// one stands, so a call that glows is given a shape of its own.
    /// </para>
    /// </summary>
    public bool GivesLight => false;

    /// <summary>
    /// This method is used to execute the resolver to produce a value as a surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public Surface ResolveToSurface(RenderContext context, Variables variables);

    /// <summary>
    /// This method lays what this resolver says over a surface already made, rather than making a new
    /// one.
    /// <para>
    /// It exists for one case.  When a scene calls a primitive of its own and adds a block after the
    /// call, two sets of names are in play: the primitive's body belongs to where the primitive was
    /// written, and the block belongs to where the call was.  A loop placing a row of them says
    /// <c>translate X step</c> in that block, and <c>step</c> is the caller's.  So the body is made
    /// first in its own names, and the block laid over it in the caller's.
    /// </para>
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="surface">The surface already made.</param>
    public void ApplyToSurface(RenderContext context, Variables variables, Surface surface);
}
