using RayTracer.General;
using RayTracer.ImageIO;
using RayTracer.Patterns;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This class is used to resolve a pattern read from a picture.
/// </summary>
public class ImagePatternResolver : PatternResolver<ImagePattern>
{
    /// <summary>
    /// This property holds the resolver for the picture to read.
    /// </summary>
    public Resolver<ImageReference> ImageReferenceResolver { get; init; }

    /// <summary>
    /// This property holds the map the picture is laid by, if one was given.
    /// </summary>
    public ImageMapType? MapType { get; init; }

    /// <summary>
    /// This property notes whether the picture is laid once rather than repeated.
    /// </summary>
    public bool Once { get; init; }

    /// <summary>
    /// This property notes which of the picture's channels the pattern takes its value from.
    /// </summary>
    public ImageChannel Channel { get; init; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, ImagePattern value)
    {
        value.ImageReference = ImageReferenceResolver.Resolve(context, variables);
        value.MapType = MapType;
        value.Once = Once;
        value.Channel = Channel;

        base.SetProperties(context, variables, value);

        value.Load();
    }
}
