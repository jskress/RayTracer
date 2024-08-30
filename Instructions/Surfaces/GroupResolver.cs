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
    /// This property holds the resolver for the bounding box property of our group.
    /// </summary>
    public BoundingBoxResolver BoundingBoxResolver { get; set; }

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
        Interval interval = GroupInterval?.GetInterval(variables) ??
                            new Interval { Start = 1, End = 1 };
        string variableName = GroupInterval?.VariableName;

        while (!interval.IsAtEnd)
        {
            double index = interval.Next();

            if (variableName != null)
                variables.SetValue(variableName, index);

            CreateChildSurfaces(context, variables, value);
        }

        // Push our material to interested children, if we have one.  If not, this will
        // be filled in by our parent, and so on...
        if (value.Material != null)
            SetMaterial(value.Surfaces, value.Material);
        
        // Now, roll up our bounding box.
        SetBoundingBox(value);

        BoundingBoxResolver.AssignTo(value, target => target.BoundingBox, context, variables);

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
        group.Surfaces.AddRange(SurfaceResolvers
            .Select(surface => surface.ResolveToSurface(context, variables)));
    }

    /// <summary>
    /// This is a helper method that will push the given material down to all descendents
    /// who want it.  It will recurse as necessary.
    /// </summary>
    /// <param name="surfaces">The list of surfaces to apply the material to.</param>
    /// <param name="material">The material to apply.</param>
    private static void SetMaterial(List<Surface> surfaces, Material material)
    {
        foreach (Surface surface in surfaces)
            surface.SetMaterial(material);
    }

    /// <summary>
    /// This is a helper method that will set the bounding box as needed, base on the
    /// group's children.
    /// </summary>
    private static void SetBoundingBox(Group parent)
    {
        BoundingBox boundingBox = null;

        foreach (Surface surface in parent.Surfaces)
        {
            switch (surface)
            {
                case Group group when boundingBox == null:
                    boundingBox = group.BoundingBox;
                    break;
                case Group group:
                    boundingBox.Add(group.BoundingBox);
                    break;
                case Triangle triangle:
                {
                    boundingBox ??= new BoundingBox();

                    boundingBox.Add(triangle.Point1);
                    boundingBox.Add(triangle.Point2);
                    boundingBox.Add(triangle.Point3);
                    break;
                }
            }
        }

        boundingBox?.Adjust();

        parent.BoundingBox = boundingBox;
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
