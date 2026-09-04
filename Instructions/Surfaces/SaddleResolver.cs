using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a saddle value.  Its only properties are how far it reaches, since
/// scaling gives every pair of curvatures there is.
/// </summary>
public class SaddleResolver : SurfaceResolver<Saddle>
{
    /// <summary>
    /// This property holds the resolver for how far the sheet reaches along X.
    /// </summary>
    public Resolver<double> WidthResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far the sheet reaches along Z.
    /// </summary>
    public Resolver<double> DepthResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a saddle.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Saddle value)
    {
        WidthResolver.AssignTo(value, target => target.Width, context, variables);
        DepthResolver.AssignTo(value, target => target.Depth, context, variables);

        base.SetProperties(context, variables, value);
    }
}
