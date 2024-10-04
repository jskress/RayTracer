using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a parallelogram value.
/// </summary>
public class ParallelogramResolver : SurfaceResolver<Parallelogram>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the "anchor" point property on a parallelogram.
    /// </summary>
    public Resolver<Point> PointResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the side 1 property on a parallelogram.
    /// </summary>
    public Resolver<Vector> Side1Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the side 2 property on a parallelogram.
    /// </summary>
    public Resolver<Vector> Side2Resolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an
    /// extruded surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Parallelogram value)
    {
        PointResolver.AssignTo(value, target => target.Point, context, variables);
        Side1Resolver.AssignTo(value, target => target.Side1, context, variables);
        Side2Resolver.AssignTo(value, target => target.Side2, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (PointResolver is null)
            return "The \"at\" property is required.";

        return Side1Resolver is null || Side2Resolver is null
            ? "The \"sides\" property is required."
            : null;
    }
}
