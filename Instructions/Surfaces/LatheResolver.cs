using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions.Surfaces.Extrusions;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a lathe value.
/// </summary>
public class LatheResolver : SurfaceResolver<Lathe>
{
    /// <summary>
    /// This property holds the resolver for the path property on a lathe.
    /// </summary>
    public GeneralPathResolver GeneralPathResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a lathe.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Lathe value)
    {
        GeneralPathResolver.AssignTo(value, target => target.Path, context, variables);

        base.SetProperties(context, variables, value);
    }
}
