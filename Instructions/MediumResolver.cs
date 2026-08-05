using Lex.Parser;
using RayTracer.Core;
using RayTracer.Fields;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions;

/// <summary>
/// This class is used to resolve what fills a piece of space.
/// </summary>
public class MediumResolver : ObjectResolver<Medium>
{
    /// <summary>
    /// This property holds the resolver for how much light the medium absorbs per unit of distance.
    /// </summary>
    public Resolver<Color> AbsorptionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much light the medium gives off per unit of
    /// distance.
    /// </summary>
    public Resolver<Color> EmissionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much light the medium turns aside per unit of
    /// distance.
    /// </summary>
    public Resolver<Color> ScatteringResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for which way the medium prefers to turn light.
    /// </summary>
    public Resolver<double> AnisotropyResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for which shape of scattering the medium follows.
    /// </summary>
    public Resolver<PhaseFunction> PhaseFunctionResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many places along a crossing the medium is asked what
    /// light reaches it, when the medium names a count of its own.
    /// </summary>
    public Resolver<int> SamplesResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much of the medium there is.
    /// </summary>
    public Resolver<double> DensityResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much of it there is from place to place, when the
    /// medium has a shape rather than an even density.
    /// </summary>
    public Resolver<FieldExpression> DensityFieldResolver { get; set; }

    /// <summary>
    /// This property holds the check, if any, to make of the medium once it is built.  Where a medium
    /// may go decides what it may say -- the surroundings have no far side, and a medium filling them
    /// must be one that has an answer over an endless span -- so the check belongs to the place the
    /// medium was written rather than to media at large.
    /// </summary>
    public Func<Medium, string> Validator { get; set; }

    /// <summary>
    /// This method is used to execute the resolver to produce a medium, and to hold it to whatever
    /// the place it was written asks of it.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <returns>The medium the block describes.</returns>
    public override Medium Resolve(RenderContext context, Variables variables)
    {
        Medium medium = base.Resolve(context, variables);
        string message = Validator?.Invoke(medium);

        if (message != null)
            throw new TokenException(message);

        return medium;
    }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a medium.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Medium value)
    {
        AbsorptionResolver.AssignTo(value, target => target.Absorption, context, variables);
        EmissionResolver.AssignTo(value, target => target.Emission, context, variables);
        ScatteringResolver.AssignTo(value, target => target.Scattering, context, variables);
        AnisotropyResolver.AssignTo(value, target => target.Anisotropy, context, variables);
        PhaseFunctionResolver.AssignTo(value, target => target.PhaseFunction, context, variables);
        DensityResolver.AssignTo(value, target => target.Density, context, variables);

        // Compiled here rather than at render time: a shape is a real delegate over three numbers, and
        // making one is the same work whether it is done now or on the first ray to want it.
        if (DensityFieldResolver is not null)
        {
            value.DensityField = FieldFunction.Compile(
                DensityFieldResolver.Resolve(context, variables));
        }

        // How hard to work is the context's business, and the medium's only when it says so: a scene
        // with one dense plume among thin haze can spend its samples where they are wanted.
        value.Samples = SamplesResolver is null
            ? context.MediumSamples
            : SamplesResolver.Resolve(context, variables);
    }
}
