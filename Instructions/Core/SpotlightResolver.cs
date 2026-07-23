using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve a spotlight value.
/// </summary>
public class SpotlightResolver : NamedObjectResolver<Spotlight>
{
    /// <summary>
    /// This property holds the resolver for where the spotlight stands.
    /// </summary>
    public Resolver<Point> LocationResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the point the spotlight is aimed at.
    /// </summary>
    public Resolver<Point> PointAtResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the half-angle of the fully-lit inner cone.
    /// </summary>
    public Resolver<double> RadiusResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the half-angle at which the light reaches nothing.
    /// </summary>
    public Resolver<double> FalloffResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how sharply the light gathers toward the axis.
    /// </summary>
    public Resolver<double> TightnessResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the light's colour.
    /// </summary>
    public Resolver<Color> ColorResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a spotlight.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, Spotlight value)
    {
        base.SetProperties(context, variables, value);

        LocationResolver.AssignTo(value, target => target.Location, context, variables);
        PointAtResolver.AssignTo(value, target => target.PointAt, context, variables);
        RadiusResolver.AssignTo(value, target => target.Radius, context, variables);
        FalloffResolver.AssignTo(value, target => target.Falloff, context, variables);
        TightnessResolver.AssignTo(value, target => target.Tightness, context, variables);
        ColorResolver.AssignTo(value, target => target.Color, context, variables);
    }
}
