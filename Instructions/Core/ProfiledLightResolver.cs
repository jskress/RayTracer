using RayTracer.Basics;
using RayTracer.Core;
using RayTracer.General;

namespace RayTracer.Instructions.Core;

/// <summary>
/// This class is the base for resolving a light that stands at a place and may be given a profile: a point light, a
/// spotlight or an area light; see <see cref="LightProfile"/>.
/// </summary>
/// <typeparam name="TLight">The kind of light.</typeparam>
public abstract class ProfiledLightResolver<TLight> : FadingLightResolver<TLight>
    where TLight : PointLight, new()
{
    /// <summary>
    /// This property holds the resolver for the name of the light's profile, if it has one.
    /// </summary>
    public Resolver<string> ProfileResolver { get; set; }

    /// <summary>
    /// This property holds the folder a profile's name is found from: the scene's own.
    /// </summary>
    public string Directory { get; set; }

    /// <summary>
    /// This property holds the resolver for how far the profile is turned about its straight down.
    /// </summary>
    public Resolver<double> ProfileTurnResolver { get; set; }

    /// <summary>
    /// This property holds the resolver for where the profile's straight down points.
    /// </summary>
    public Resolver<Point> ProfileAimResolver { get; set; }

    /// <summary>
    /// This method is used to apply our resolvers to the appropriate properties of a light.
    /// </summary>
    /// <param name="context">The current render context.</param>
    /// <param name="variables">The current set of scoped variables.</param>
    /// <param name="value">The value to update.</param>
    protected override void SetProperties(RenderContext context, Variables variables, TLight value)
    {
        base.SetProperties(context, variables, value);

        if (ProfileResolver?.Resolve(context, variables) is { } name)
            value.Profile = LightProfile.From(Path.Combine(Directory ?? "", name));

        ProfileTurnResolver.AssignTo(value, target => target.ProfileTurn, context, variables);
        ProfileAimResolver.AssignTo(value, target => target.ProfileAim, context, variables);
    }
}
