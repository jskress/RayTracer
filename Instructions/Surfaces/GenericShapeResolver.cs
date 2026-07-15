using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.Instructions.Surfaces.Extrusions;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a generic shape value.
/// </summary>
public class GenericShapeResolver : SurfaceResolver<GenericShape>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for the path property on a generic shape.
    /// </summary>
    public GeneralPathResolver PathResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a
    /// generic shape.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, GenericShape value)
    {
        PathResolver.AssignTo(value, target => target.Path, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return PathResolver is null
            ? "The \"path\" property is required."
            : null;
    }
}
