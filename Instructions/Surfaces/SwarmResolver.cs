using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a swarm: the thing every copy is, and how copies are thrown and fly.
/// </summary>
public class SwarmResolver : SurfaceResolver<Swarm>, IValidatable
{
    public ISurfaceResolver ThingResolver { get; set; }
    public Resolver<Point> FromResolver { get; set; }
    public Resolver<Vector> TowardResolver { get; set; }
    public Resolver<double> SpreadResolver { get; set; }
    public Resolver<double> SpeedFromResolver { get; set; }
    public Resolver<double> SpeedToResolver { get; set; }
    public Resolver<double> RateResolver { get; set; }
    public Resolver<double> SinceResolver { get; set; }
    public Resolver<double> LifeFromResolver { get; set; }
    public Resolver<double> LifeToResolver { get; set; }
    public Resolver<double> BounceResolver { get; set; }
    public Resolver<double> FrictionResolver { get; set; }
    public Resolver<double> DragResolver { get; set; }
    public Resolver<double> RadiusResolver { get; set; }
    public ISurfaceResolver WithinResolver { get; set; }
    public Resolver<double> SpinFromResolver { get; set; }
    public Resolver<double> SpinToResolver { get; set; }
    public Resolver<double> FallsFromResolver { get; set; }
    public Resolver<double> FallsToResolver { get; set; }

    protected override void SetProperties(RenderContext context, Variables variables, Swarm value)
    {
        base.SetProperties(context, variables, value);

        value.Thing = ThingResolver?.ResolveToSurface(context, variables);
        value.Within = WithinResolver?.ResolveToSurface(context, variables);
        FromResolver.AssignTo(value, target => target.From, context, variables);
        TowardResolver.AssignTo(value, target => target.Toward, context, variables);
        SpreadResolver.AssignTo(value, target => target.Spread, context, variables);
        RateResolver.AssignTo(value, target => target.Rate, context, variables);
        SinceResolver.AssignTo(value, target => target.Since, context, variables);
        BounceResolver.AssignTo(value, target => target.Bounce, context, variables);
        FrictionResolver.AssignTo(value, target => target.Friction, context, variables);
        DragResolver.AssignTo(value, target => target.Drag, context, variables);
        value.Radius = RadiusResolver?.Resolve(context, variables);

        if (SpeedFromResolver is not null)
        {
            value.SpeedFrom = SpeedFromResolver.Resolve(context, variables);
            value.SpeedTo = SpeedToResolver?.Resolve(context, variables) ?? value.SpeedFrom;
        }

        if (LifeFromResolver is not null)
        {
            value.LifeFrom = LifeFromResolver.Resolve(context, variables);
            value.LifeTo = LifeToResolver?.Resolve(context, variables) ?? value.LifeFrom;
        }

        if (SpinFromResolver is not null)
        {
            value.SpinFrom = SpinFromResolver.Resolve(context, variables);
            value.SpinTo = SpinToResolver?.Resolve(context, variables) ?? value.SpinFrom;
        }

        if (FallsFromResolver is not null)
        {
            value.FallsFrom = FallsFromResolver.Resolve(context, variables);
            value.FallsTo = FallsToResolver?.Resolve(context, variables) ?? value.FallsFrom;
        }
    }

    /// <summary>
    /// This method validates the state of the object and returns the text of any error message, or <c>null</c>,
    /// if all is well: a swarm with nothing to throw drew nothing, and said nothing about it.
    /// </summary>
    /// <returns>The text of an error message or <c>null</c>.</returns>
    public string Validate() =>
        ThingResolver is null ? "A swarm needs something to throw: say what with \"of\"." : null;
}
