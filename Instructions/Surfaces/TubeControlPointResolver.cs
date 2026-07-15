using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a tube control point value.
/// </summary>
public class TubeControlPointResolver : ObjectResolver<TubeControlPoint>
{
    /// <summary>
    /// This property holds the resolver for the center property of our control point.
    /// </summary>
    public Resolver<Point> CenterResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the radius property of our control point.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// control point.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TubeControlPoint value)
    {
        CenterResolver.AssignTo(value, target => target.Center, context, variables);
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
    }
}
