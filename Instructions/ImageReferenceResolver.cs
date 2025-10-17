using RayTracer.General;
using RayTracer.ImageIO;

namespace RayTracer.Instructions;

/// <summary>
/// This class provides a resolver for image references.
/// </summary>
public class ImageReferenceResolver : ObjectResolver<ImageReference>
{
    /// <summary>
    /// This property holds the resolver for our image's name property.
    /// </summary>
    public TermResolver<string> ImageNameResolver { get; init; }

    /// <summary>
    /// This property holds the resolver for our image's source directory property.
    /// </summary>
    public Resolver<string> SourceDirectoryResolver { get; init; }

    /// <summary>
    /// This property holds the resolver for our image's always load property.
    /// </summary>
    public Resolver<bool> AlwaysLoadResolver { get; init; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an image
    /// reference.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, ImageReference value)
    {
        ImageNameResolver.AssignTo(value, target => target.ImageName, context, variables);
        SourceDirectoryResolver.AssignTo(value, target => target.SourceDirectory, context, variables);
        AlwaysLoadResolver.AssignTo(value, target => target.AlwaysLoad, context, variables);
    }
}
