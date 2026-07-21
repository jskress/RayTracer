using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve a mottled pigment value.
/// </summary>
public class MottledPigmentResolver : PigmentResolver<MottledPigment>
{
    /// <summary>
    /// This property holds the resolver for our pigment's wrapped pigment property.
    /// </summary>
    public IPigmentResolver PigmentResolver { get; init; }

    /// <summary>
    /// This property holds the resolver for the noise this pigment dims its wrapped pigment by.
    /// </summary>
    public Resolver<LayeredNoise> NoiseResolver { get; init; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override MottledPigment Resolve(RenderContext context, Variables variables)
    {
        MottledPigment pigment = new ()
        {
            Pigment = PigmentResolver.ResolveToPigment(context, variables),
            Noise = NoiseResolver.Resolve(context, variables)
        };

        SetProperties(context, variables, pigment);

        return pigment;
    }
}
