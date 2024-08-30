using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a value that is an extruded surface.
/// </summary>
public class ExtrudedSurfaceResolver<TValue> : SurfaceResolver<TValue>
    where TValue : ExtrudedSurface, new()
{
    /// <summary>
    /// This property holds the resolver for the minimum Y property on an extruded surface.
    /// </summary>
    public Resolver<double> MinimumYResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the maximum Y property on an extruded surface.
    /// </summary>
    public Resolver<double> MaximumYResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the closed property on an extruded surface.
    /// </summary>
    public Resolver<bool> ClosedResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an
    /// extruded surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TValue value)
    {
        MinimumYResolver.AssignTo(value, target => target.MinimumY, context, variables);
        MaximumYResolver.AssignTo(value, target => target.MaximumY, context, variables);
        ClosedResolver.AssignTo(value, target => target.Closed, context, variables);

        base.SetProperties(context, variables, value);
    }
}
