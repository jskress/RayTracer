using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve the weather a group or object has been out in: grime, dust and wear.
/// </summary>
public class WeatherResolver : ObjectResolver<Weather>
{
    /// <summary>
    /// This property holds the resolver for how much grime has gathered.
    /// </summary>
    public Resolver<double> GrimeResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the grime's color.
    /// </summary>
    public Resolver<Color> GrimeColorResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how much dust has settled.
    /// </summary>
    public Resolver<double> DustResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the dust's color.
    /// </summary>
    public Resolver<Color> DustColorResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far the edges are worn.
    /// </summary>
    public Resolver<double> WearResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for what wear bares.
    /// </summary>
    public Resolver<Material> WearToResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how far about a place grime and wear are looked for.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how high dirt has been kicked up from the ground.
    /// </summary>
    public Resolver<double> SplashResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the kicked-up dirt's color.
    /// </summary>
    public Resolver<Color> SplashColorResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of the weather.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The weather to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Weather value)
    {
        GrimeResolver.AssignTo(value, target => target.Grime, context, variables);
        GrimeColorResolver.AssignTo(value, target => target.GrimeColor, context, variables);
        DustResolver.AssignTo(value, target => target.Dust, context, variables);
        DustColorResolver.AssignTo(value, target => target.DustColor, context, variables);
        WearResolver.AssignTo(value, target => target.Wear, context, variables);
        WearToResolver.AssignTo(value, target => target.WearTo, context, variables);
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
        SplashResolver.AssignTo(value, target => target.Splash, context, variables);
        SplashColorResolver.AssignTo(value, target => target.SplashColor, context, variables);
    }
}
