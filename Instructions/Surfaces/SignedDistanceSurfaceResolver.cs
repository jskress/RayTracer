using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a signed distance surface.
/// </summary>
public class SignedDistanceSurfaceResolver : SurfaceResolver<SignedDistanceSurface>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the arithmetic that gives the distance.
    /// </summary>
    public FieldExpressionResolver FunctionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how closely a crossing is pinned down.
    /// </summary>
    public Resolver<double> AccuracyResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(
        RenderContext context, Variables variables, SignedDistanceSurface value)
    {
        FunctionResolver.AssignTo(value, target => target.Function, context, variables);
        AccuracyResolver.AssignTo(value, target => target.Accuracy, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or
    /// <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (FunctionResolver is null)
            return "A signed distance surface needs a \"function\".";

        return BoundingBoxResolver is null
            ? "A signed distance surface needs a \"bounded by\", since nothing about a piece of " +
              "arithmetic says where to stop looking for its surface."
            : null;
    }
}
