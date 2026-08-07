using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is the base for resolving lights that stand somewhere, and so may thin with the
/// distance their light has travelled.
/// <para>
/// A sun and a sky are left out of this deliberately rather than by oversight: both are infinitely
/// far off, so nothing in a scene is nearer to them than anything else and a fading distance would
/// have nothing to measure against.
/// </para>
/// </summary>
/// <typeparam name="TLight">The type of light being resolved.</typeparam>
public abstract class FadingLightResolver<TLight> : NamedObjectResolver<TLight>
    where TLight : Light, new()
{
    /// <summary>
    /// This property holds the resolver for the distance at which the light's color means what it
    /// says.
    /// </summary>
    public Resolver<double> FadeDistanceResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how quickly the light thins past that distance.
    /// </summary>
    public Resolver<double> FadePowerResolver { get; set; }

    /// <summary>
    /// This method is used to apply the fading resolvers to the light.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TLight value)
    {
        base.SetProperties(context, variables, value);

        if (FadeDistanceResolver is not null)
            value.FadeDistance = FadeDistanceResolver.Resolve(context, variables);

        FadePowerResolver.AssignTo(value, target => target.FadePower, context, variables);
    }
}
