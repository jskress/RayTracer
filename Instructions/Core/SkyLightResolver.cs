using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;
using RayTracer.Instructions.Pigments;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a sky light value.
/// </summary>
public class SkyLightResolver : NamedObjectResolver<SkyLight>
{
    /// <summary>
    /// This property holds the resolver for the color the light carries, which multiplies whatever the
    /// sky itself is rather than replacing it.
    /// </summary>
    public Resolver<Color> ColorResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the sky this light carries, when the scene gives it one of
    /// its own rather than letting it borrow the background.
    /// </summary>
    public IPigmentResolver PigmentResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many directions the sky is looked at from.
    /// </summary>
    public Resolver<int> SamplesResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a sky light.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, SkyLight value)
    {
        ColorResolver.AssignTo(value, target => target.Color, context, variables);
        SamplesResolver.AssignTo(value, target => target.Samples, context, variables);

        // Left as nothing when the scene named none, so that the scene may hand it the background once
        // everything is in place.
        if (PigmentResolver is not null)
            value.Pigment = PigmentResolver.ResolveToPigment(context, variables);

        base.SetProperties(context, variables, value);
    }
}
