using RayTracer.Basics;
using RayTracer.General;
using RayTracer.Geometry;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve a swarm: the thing every copy is, and how copies are thrown and fly.
/// </summary>
public class SwarmResolver : SurfaceResolver<Swarm>
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

    protected override void SetProperties(RenderContext context, Variables variables, Swarm value)
    {
        base.SetProperties(context, variables, value);

        value.Thing = ThingResolver?.ResolveToSurface(context, variables);
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
    }
}
