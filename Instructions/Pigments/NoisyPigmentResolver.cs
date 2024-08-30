using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Pigments;

namespace RayTracer.Instructions.Pigments;

/// <summary>
/// This class is used to resolve a noisy pigment value.
/// </summary>
public class NoisyPigmentResolver : PigmentResolver<NoisyPigment>
{
    /// <summary>
    /// This property holds the resolver for our pigment's wrapped pigment property.
    /// </summary>
    public IPigmentResolver PigmentResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for our pigment's turbulence property.
    /// </summary>
    public Resolver<Turbulence> TurbulenceResolver { get; set; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public override NoisyPigment Resolve(RenderContext context, Variables variables)
    {
        NoisyPigment pigment = new ()
        {
            Pigment = PigmentResolver.ResolveToPigment(context, variables),
            Turbulence = TurbulenceResolver.Resolve(context, variables)
        };

        SetProperties(context, variables, pigment);

        return pigment;
    }
}
