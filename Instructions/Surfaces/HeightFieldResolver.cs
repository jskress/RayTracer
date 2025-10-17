using RayTracer.General;
using RayTracer.Geometry;
using RayTracer.ImageIO;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a height field value.
/// </summary>
public class HeightFieldResolver : SurfaceResolver<HeightField>, IValidatable
{
    /// <summary>
    /// This property holds the resolver for our pigment's image resolver property.
    /// </summary>
    public Resolver<ImageReference> ImageReferenceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the clip property on a height field.
    /// </summary>
    public TermResolver<double> ClipResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the closed property on a height field.
    /// </summary>
    public Resolver<bool> ClosedResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an
    /// extruded surface.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, HeightField value)
    {
        ImageReferenceResolver.AssignTo(value, target => target.ImageReference, context, variables);
        ClosedResolver.AssignTo(value, target => target.Closed, context, variables);
        ClipResolver.AssignTo(value, target => target.Clip, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        return ImageReferenceResolver is null
            ? "The \"image\" property is required."
            : null;
    }
}
