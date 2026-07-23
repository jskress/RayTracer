using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;
using RayTracer.Graphics;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is used to resolve an area light value.
/// </summary>
public class AreaLightResolver : NamedObjectResolver<AreaLight>
{
    /// <summary>
    /// This property holds the resolver for where the centre of the lit face stands.
    /// </summary>
    public Resolver<Point> LocationResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for one full edge of the lit face.
    /// </summary>
    public Resolver<Vector> Axis1Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the other full edge of the lit face.
    /// </summary>
    public Resolver<Vector> Axis2Resolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many samples are taken across the first axis.
    /// </summary>
    public Resolver<int> UStepsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for how many samples are taken across the second axis.
    /// </summary>
    public Resolver<int> VStepsResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for the seed the jitter should use.
    /// </summary>
    public Resolver<int?> SeedResolver { get; set; }

    /// <summary>
    /// This property notes whether the samples are jittered.  It is set straight rather than
    /// resolved, since it comes from the bare presence of "no jitter" rather than an expression.
    /// </summary>
    public bool? Jitter { get; set; }

    /// <summary>
    /// This property holds the resolver for the light's colour.
    /// </summary>
    public Resolver<Color> ColorResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of an area light.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, AreaLight value)
    {
        base.SetProperties(context, variables, value);

        LocationResolver.AssignTo(value, target => target.Location, context, variables);
        Axis1Resolver.AssignTo(value, target => target.Axis1, context, variables);
        Axis2Resolver.AssignTo(value, target => target.Axis2, context, variables);
        UStepsResolver.AssignTo(value, target => target.USteps, context, variables);
        VStepsResolver.AssignTo(value, target => target.VSteps, context, variables);
        SeedResolver.AssignTo(value, target => target.Seed, context, variables);
        ColorResolver.AssignTo(value, target => target.Color, context, variables);

        if (Jitter.HasValue)
            value.Jitter = Jitter.Value;
    }
}
