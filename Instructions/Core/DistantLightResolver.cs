using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a distant light value.
/// </summary>
public class DistantLightResolver : NamedObjectResolver<DistantLight>
{
    /// <summary>
    /// This property holds the resolver for the direction the light travels.
    /// </summary>
    public Resolver<Vector> DirectionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the light's colour.
    /// </summary>
    public Resolver<Color> ColorResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a distant light.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(
        RenderContext context, Variables variables, DistantLight value)
    {
        base.SetProperties(context, variables, value);

        DirectionResolver.AssignTo(value, target => target.Direction, context, variables);
        ColorResolver.AssignTo(value, target => target.Color, context, variables);
    }
}
