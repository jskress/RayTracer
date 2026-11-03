using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a tube value.
/// </summary>
public class TubeResolver : SurfaceResolver<Tube>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the point the tube starts at.
    /// </summary>
    public TubeControlPointResolver StartResolver { get; set; }

    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of segments
    /// that carry the tube from its start point to its end.
    /// </summary>
    public List<TubeSegmentSpecResolver> SegmentResolvers { get; private set; } = [];

    /// <summary>
    /// This property holds the resolver for whether to suppress the tangent-continuity
    /// check on this tube's control points.
    /// </summary>
    public Resolver<bool> DiscontinuousResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the spline the tube runs along, in place of its own control points: a
    /// cable, a hose, a wire hung in a catenary.
    /// </summary>
    public Resolver<Spline> SplineResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how thick a tube along a spline is, all along it.
    /// </summary>
    public Resolver<double> SplineRadiusResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a tube.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Tube value)
    {
        if (SplineResolver is not null)
            Along(SplineResolver.Resolve(context, variables), SplineRadiusResolver.Resolve(context, variables), value);
        else
        {
            value.Start = StartResolver.Resolve(context, variables);
            value.Segments.AddRange(SegmentResolvers
                .Select(resolver => resolver.Resolve(context, variables)));
        }

        DiscontinuousResolver.AssignTo(value, target => target.Discontinuous, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (SplineResolver is not null)
        {
            return StartResolver is not null
                ? "A tube runs along a spline or through control points of its own, not both."
                : null;
        }

        return StartResolver is null || SegmentResolvers is null || SegmentResolvers.Count == 0
            ? "A tube needs at least two control points."
            : null;
    }

    /// <summary>
    /// This method lays a tube along a spline, segment for segment, as thick all along as it is told -- a spline's
    /// kinks, said on purpose, carried over with it.
    /// </summary>
    private static void Along(Spline spline, double radius, Tube tube)
    {
        if (radius <= 0)
            throw new Exception("A tube's radius must be more than nought.");

        TubeControlPoint At(Point center) => center is null ? null : new TubeControlPoint { Center = center, Radius = radius };

        tube.Start = At(spline.Start);
        tube.Segments.AddRange(spline.Segments.Select(segment => new TubeSegmentSpec
        {
            Control1 = At(segment.Control1), Control2 = At(segment.Control2), End = At(segment.End)
        }));
        tube.Discontinuous = spline.Discontinuous;
    }

    /// <summary>
    /// This method creates a copy of this resolver.
    /// </summary>
    /// <returns>A clone of this resolver.</returns>
    public override object Clone()
    {
        TubeResolver resolver = (TubeResolver) base.Clone();

        // Force the list to be physically different, but with the same content.
        resolver.SegmentResolvers = [..resolver.SegmentResolvers];

        return resolver;
    }
}
