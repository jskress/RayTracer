using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a tube segment spec value.
/// </summary>
public class TubeSegmentSpecResolver : ObjectResolver<TubeSegmentSpec>
{
    /// <summary>
    /// This property holds the resolver for the segment's first curve control point, or
    /// <c>null</c>, for a straight line.
    /// </summary>
    public TubeControlPointResolver Control1Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the segment's second curve control point, or
    /// <c>null</c>, for a straight line or a quadratic curve.
    /// </summary>
    public TubeControlPointResolver Control2Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the point this segment ends at.
    /// </summary>
    public TubeControlPointResolver EndResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// segment spec.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TubeSegmentSpec value)
    {
        if (Control1Resolver != null)
            value.Control1 = Control1Resolver.Resolve(context, variables);

        if (Control2Resolver != null)
            value.Control2 = Control2Resolver.Resolve(context, variables);

        value.End = EndResolver.Resolve(context, variables);
    }
}
