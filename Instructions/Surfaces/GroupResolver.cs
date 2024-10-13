using RayTracer.Basics;
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
    /// This property holds the group interval, if any, we are to use in iterating over
    /// the group.
    /// </summary>
    public GroupInterval GroupInterval { get; set; }

    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of surfaces
    /// for our group.
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
        Interval interval = GroupInterval?.GetInterval(variables) ?? Interval.Once;
        string variableName = GroupInterval?.VariableName;

        while (!interval.IsAtEnd)
        {
            double index = interval.Next();

            if (variableName != null)
                variables.SetValue(variableName, index);

            CreateChildSurfaces(context, variables, value);
        }

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method will iterate over our surface resolvers and add the created surfaces
    /// to our group.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="group">The group to add things to.</param>
    private void CreateChildSurfaces(RenderContext context, Variables variables, Group group)
    {
        SurfaceResolvers
            .Select(surface => surface.ResolveToSurface(context, variables))
            .ToList()
            .ForEach(surface => group.Add(surface));
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
