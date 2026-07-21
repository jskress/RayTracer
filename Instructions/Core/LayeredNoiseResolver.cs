using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class resolves the layers common to noise and turbulence alike: how many there are, and
/// how much finer and fainter each is than the one before.
/// </summary>
/// <typeparam name="TValue">The kind of noise being resolved.</typeparam>
public class LayeredNoiseResolver<TValue> : ObjectResolver<TValue>
    where TValue : LayeredNoise, new()
{
    /// <summary>
    /// This is a resolver that produces noise of a single layer, which is an effective no-op
    /// around one call to the noise function.
    /// </summary>
    public static readonly LayeredNoiseResolver<TValue> NoLayersResolver = new ()
    {
        OctavesResolver = new LiteralResolver<int> { Value = 0 }
    };

    /// <summary>
    /// This property holds the resolver for the noise's seed.
    /// </summary>
    public Resolver<int?> SeedResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many layers of noise are summed.
    /// </summary>
    public Resolver<int> OctavesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much finer each layer is than the last.
    /// </summary>
    public Resolver<double> FinerResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much fainter each layer is than the last.
    /// </summary>
    public Resolver<double> FainterResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the noise.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TValue value)
    {
        SeedResolver.AssignTo(value, target => target.Seed, context, variables);
        OctavesResolver.AssignTo(value, target => target.Octaves, context, variables);
        FinerResolver.AssignTo(value, target => target.Finer, context, variables);
        FainterResolver.AssignTo(value, target => target.Fainter, context, variables);
    }
}
