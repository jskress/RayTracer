using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a point light value.
/// </summary>
public class PointLightResolver : NamedObjectResolver<PointLight>
{
    /// <summary>
    /// This property holds the resolver for our camera's location property.
    /// </summary>
    public Resolver<Point> LocationResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our camera's color property.
    /// </summary>
    public Resolver<Color> ColorResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a camera.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, PointLight value)
    {
        base.SetProperties(context, variables, value);
        
        LocationResolver.AssignTo(value, target => target.Location, context, variables);
        ColorResolver.AssignTo(value, target => target.Color, context, variables);
    }
}
