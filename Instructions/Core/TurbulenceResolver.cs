using RayTracer.Basics;
using RayTracer.General;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a turbulence value, which is layered noise plus how far it may
/// push the points it is asked to stir.
/// </summary>
public class TurbulenceResolver : LayeredNoiseResolver<Turbulence>
{
    /// <summary>
    /// This is a default turbulence resolver that stirs nothing at all.
    /// </summary>
    public static readonly TurbulenceResolver NoiseWithNoTurbulenceResolver = new ()
    {
        OctavesResolver = new LiteralResolver<int> { Value = 0 }
    };

    /// <summary>
    /// This property holds the resolver for how far a point may be pushed.  It is typed loosely
    /// because it may be written as one number, meaning the same on every axis, or as a triple
    /// giving each its own.
    /// </summary>
    public Resolver<object> AmplitudeResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a turbulence.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Turbulence value)
    {
        base.SetProperties(context, variables, value);

        // One number means the same push on every axis; a triple gives each its own.  Spelling this
        // out here rather than leaning on coercion is deliberate -- teaching the general converter
        // that a number may stand in for a triple would quietly change what "translate 3" means.
        if (AmplitudeResolver is null)
            return;

        object amount = AmplitudeResolver.Resolve(context, variables);

        value.Amplitude = amount switch
        {
            Vector vector => vector,
            double number => new Vector(number, number, number),
            _ => value.Amplitude
        };
    }
}
