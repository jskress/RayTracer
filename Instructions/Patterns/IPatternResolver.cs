using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Patterns;

namespace RayTracer.Instructions.Patterns;

/// <summary>
/// This interface defines the contract for something that can resolve to a pattern. 
/// </summary>
public interface IPatternResolver
{
    /// <summary>
    /// This property holds the resolver for the turbulence to stir a pattern's points with.  It
    /// lives on the interface so that the parser may set it for any pattern at all, which is the
    /// whole point: turbulence belongs to no pattern in particular.
    /// </summary>
    Resolver<Turbulence> TurbulenceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for what a pattern's value is multiplied by before it is
    /// shaped.
    /// </summary>
    Resolver<double> FrequencyResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for what is added to a pattern's value before it is shaped.
    /// </summary>
    Resolver<double> PhaseResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how a pattern's value is shaped once produced.
    /// </summary>
    Resolver<WaveType> WaveResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the exponent a polynomial wave uses.
    /// </summary>
    Resolver<double> ExponentResolver { get; set; }

    /// <summary>
    /// This method is used to execute the resolver to produce a value as a pattern.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    public Pattern ResolveToPattern(RenderContext context, Variables variables);
}
