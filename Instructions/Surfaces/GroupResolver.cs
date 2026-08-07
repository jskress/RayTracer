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

        // The counter belongs to the loop and to nothing outside it, so it is set in a scope of the
        // loop's own.  Names from further out are still seen, a scope handing on what it does not hold
        // itself, but the counter is not left lying about after the group is finished with -- and two
        // loops nested one inside the other may use the same name without treading on each other.
        //
        // This is what a scope is for, and until now the class could do it and never did: the only one
        // ever built was a single scope for a whole render.
        Variables scope = variableName is null ? variables : new Variables(variables);

        while (!interval.IsAtEnd)
        {
            double index = interval.Next();

            if (variableName != null)
                scope.SetValue(variableName, index);

            CreateChildSurfaces(context, scope, value);
        }

        // The group's own properties are settled outside the loop, and outside its scope with it.
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
