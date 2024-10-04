using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces.Extrusions;

/// <summary>
/// This class is used to resolve an extrusion value.
/// </summary>
public class ExtrusionResolver : ExtrudedSurfaceResolver<Extrusion>
{
    /// <summary>
    /// This property holds the resolver for the path property on an extrusion.
    /// </summary>
    public GeneralPathResolver GeneralPathResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an
    /// extruded surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Extrusion value)
    {
        GeneralPathResolver.AssignTo(value, target => target.Path, context, variables);

        base.SetProperties(context, variables, value);
    }
}
