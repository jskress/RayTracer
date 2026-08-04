using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve an isosurface value.
/// </summary>
public class IsosurfaceResolver : SurfaceResolver<Isosurface>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the function whose value makes the surface.
    /// </summary>
    public FieldExpressionResolver FunctionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the value of the function that makes the surface.
    /// </summary>
    public Resolver<double> ThresholdResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how closely a crossing is to be pinned down.
    /// </summary>
    public Resolver<double> AccuracyResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an isosurface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Isosurface value)
    {
        FunctionResolver.AssignTo(value, target => target.Function, context, variables);
        ThresholdResolver.AssignTo(value, target => target.Threshold, context, variables);
        AccuracyResolver.AssignTo(value, target => target.Accuracy, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or
    /// <c>null</c>, if all is well.
    /// <para>
    /// An isosurface is the one surface for which <c>bounded by</c> is not merely a hint the renderer
    /// may use to skip work: a function has no size of its own, so the box is where the surface is
    /// looked for at all, and one left out would mean looking nowhere in particular.  It is required
    /// here for that reason, and saying so now is better than a scene rendering an empty box.
    /// </para>
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (FunctionResolver is null)
            return "The \"function\" property is required.";

        return BoundingBoxResolver is null
            ? "The \"bounded by\" property is required for an isosurface, since it is the region the " +
              "surface is looked for in."
            : null;
    }
}
