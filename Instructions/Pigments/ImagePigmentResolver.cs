using Lex.Parser;
using RayTracer.General;
using RayTracer.Pigments;
using RayTracer.Terms;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve an image pigment value.
/// </summary>
public class ImagePigmentResolver : PigmentResolver<ImagePigment>
{
    /// <summary>
    /// This property holds the resolver for our pigment's image name property.
    /// </summary>
    public Term ImageName { get; init; }

    /// <summary>
    /// This property holds the resolver for our pigment's map type property.
    /// </summary>
    public Resolver<ImageMapType?> MapTypeResolver { get; init; }

    /// <summary>
    /// This property holds the resolver for our pigment's once property.
    /// </summary>
    public Resolver<bool> OnceResolver { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override ImagePigment Resolve(RenderContext context, Variables variables)
    {
        ImagePigment pigment = new ()
        {
            ImageName = ImageName.GetValue<string>(variables),
            MapType = MapTypeResolver.Resolve(context, variables),
            Once = OnceResolver.Resolve(context, variables)
        };

        SetProperties(context, variables, pigment);

        return pigment;
    }
}
