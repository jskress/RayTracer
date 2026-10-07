using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is the base of every light's resolver, and settles what every light has: a name, and
/// what light it gives -- a color, or the temperature of something glowing, and how bright.
/// </summary>
/// <typeparam name="TLight">The sort of light being resolved.</typeparam>
public abstract class LightResolver<TLight> : NamedObjectResolver<TLight>, ILightResolver
    where TLight : Light, new()
{
    /// <summary>
    /// This property holds the resolver for the light's color.
    /// </summary>
    public Resolver<Color> ColorResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the temperature, in kelvin, of the glow the light gives,
    /// which takes precedence over a color written beside it.
    /// </summary>
    public Resolver<double> TemperatureResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how bright the light is, as a factor on its color or its
    /// temperature's glow.
    /// </summary>
    public Resolver<double> BrightnessResolver { get; set; }

    protected override void SetProperties(RenderContext context, Variables variables, TLight value)
    {
        base.SetProperties(context, variables, value);

        ColorResolver.AssignTo(value, target => target.Color, context, variables);

        if (TemperatureResolver is not null)
            value.SetColorTemperature(TemperatureResolver.Resolve(context, variables));

        if (BrightnessResolver is not null)
            value.Brighten(BrightnessResolver.Resolve(context, variables));
    }

    public object Clone()
    {
        return MemberwiseClone();
    }
}
