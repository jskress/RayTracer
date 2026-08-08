using RayTracer.Core;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a group value.
/// </summary>
public class GroupResolver : SurfaceResolver<Group>
{
    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of surfaces
    /// for our group.  A loop may stand among them, and puts any number of surfaces there.
    /// </summary>
    public List<ISurfaceResolver> SurfaceResolvers { get; private set; } = [];

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a group.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Group value)
    {
        SurfaceLoop.AddAllTo(context, variables, SurfaceResolvers, surface => value.Add(surface));

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        GroupResolver resolver = (GroupResolver) base.Clone();

        // Force the lists to be physically different, but with the same content.
        resolver.SurfaceResolvers = [..resolver.SurfaceResolvers];

        return resolver;
    }
}
