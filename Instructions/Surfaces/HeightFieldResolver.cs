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
    /// This property holds the resolver for the function that gives the height, when the terrain is
    /// described rather than drawn.
    /// </summary>
    public FieldExpressionResolver FunctionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many points across the function is sampled at.
    /// </summary>
    public Resolver<int> SamplesResolver { get; set; }

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
        FunctionResolver.AssignTo(value, target => target.Function, context, variables);
        SamplesResolver.AssignTo(value, target => target.Samples, context, variables);
        ClosedResolver.AssignTo(value, target => target.Closed, context, variables);
        ClipResolver.AssignTo(value, target => target.Clip, context, variables);

        base.SetProperties(context, variables, value);
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error
    /// message, or <c>null</c>, if all is well.
    /// <para>
    /// A height field takes its heights from a picture or from a function, and the two settings that
    /// belong to one of those are refused with the other rather than quietly ignored.  A scene that
    /// sets <c>samples</c> beside an image, or <c>clip</c> beside a function, has said something it
    /// means, and being told it had no effect is worth more than a silent shrug.
    /// </para>
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate()
    {
        if (ImageReferenceResolver is null && FunctionResolver is null)
            return "One of the \"image\" or \"function\" properties is required.";

        if (ImageReferenceResolver is not null && FunctionResolver is not null)
        {
            return "A height field takes its heights from an \"image\" or from a \"function\", " +
                   "but not from both.";
        }

        if (FunctionResolver is null && SamplesResolver is not null)
        {
            return "The \"samples\" property belongs to a height field made from a function; an " +
                   "image brings its own size with it.";
        }

        return FunctionResolver is not null && ClipResolver is not null
            ? "The \"clip\" property belongs to a height field made from an image; a function can " +
              "clip itself, as in \"max(..., 0)\"."
            : null;
    }
}
