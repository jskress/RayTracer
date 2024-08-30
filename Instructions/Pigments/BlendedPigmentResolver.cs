using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve a blended pigment value.
/// </summary>
public class BlendedPigmentResolver : PigmentResolver<BlendedPigment>
{
    /// <summary>
    /// This property holds the list of resolvers that will evaluate to the list of pigments
    /// for a blended pigment's pigments property.
    /// </summary>
    public List<IPigmentResolver> PigmentResolvers { get; init; }

    /// <summary>
    /// This property holds the resolver for our pigment's layer property.
    /// </summary>
    public Resolver<bool> LayerResolver { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override BlendedPigment Resolve(RenderContext context, Variables variables)
    {
        BlendedPigment pigment = new ()
        {
            Pigments = PigmentResolvers
                .Select(resolver => resolver.ResolveToPigment(context, variables))
                .ToList(),
            Layer = LayerResolver.Resolve(context, variables)
        };

        SetProperties(context, variables, pigment);

        return pigment;
    }
}
