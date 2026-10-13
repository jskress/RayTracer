using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is a light standing in a list of surfaces: in a group, where it is written in the
/// group's own space and goes wherever the group goes, or in a loop or a choice inside one -- or at a
/// scene's top level, where a loop may now set lights out as well as surfaces.
/// <para>
/// It makes no surface.  What it makes is handed to the list's light rather than its surface, so a
/// group gets its light as a light, and a scene a light of its own.
/// </para>
/// </summary>
public class ListedLight : SurfaceListEntry
{
    /// <summary>
    /// This property holds the resolver for the light.
    /// </summary>
    public IObjectResolver Resolver { get; init; }

    /// <summary>
    /// This property holds what to call this when complaining that it is not a surface.
    /// </summary>
    protected override string Description => "A light";

    /// <summary>
    /// This method makes the light and hands it over.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The scope of the list this stands in.</param>
    /// <param name="add">What to do with each surface it makes, which is never called.</param>
    /// <param name="light">What to do with the light it makes.</param>
    public override void AddSurfacesTo(
        RenderContext context, Variables variables, Action<Surface> add, Action<Light> light)
    {
        light((Light) Resolver.ResolveToObject(context, variables));
    }

    /// <summary>
    /// This method returns a copy of this entry.
    /// </summary>
    /// <returns>A copy of this entry.</returns>
    public override object Clone()
    {
        return MemberwiseClone();
    }
}
