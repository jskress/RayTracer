using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve how light wanders under a material's surface: how far it travels inside.
/// </summary>
public class SubsurfaceResolver : ObjectResolver<Subsurface>
{
    /// <summary>
    /// This property holds the resolver for how far light travels inside before it is turned.
    /// </summary>
    public Resolver<Graphics.Color> RadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how thick a sheet of the stuff is.
    /// </summary>
    public Resolver<double?> ThicknessResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a subsurface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The subsurface to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Subsurface value)
    {
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
        ThicknessResolver.AssignTo(value, target => target.Thickness, context, variables);
    }
}
