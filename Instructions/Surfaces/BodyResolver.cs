using Lex.Parser;
using RayTracer.Basics;
using RayTracer.Core.Physics;
using RayTracer.Extensions;
using RayTracer.General;
using RayTracer.Terms;

namespace RayTracer.Instructions.Surfaces;

/// <summary>
/// This class is used to resolve what a scene says of a surface as a body.
/// </summary>
public class BodyResolver : ObjectResolver<Body>
{
    public Resolver<double> MassResolver { get; set; }
    public Resolver<Vector> VelocityResolver { get; set; }

    /// <summary>
    /// This property holds the term for how fast the body is turning about each axis, in the scene's own unit of
    /// angle a second.
    /// </summary>
    public Term SpinTerm { get; set; }

    public Resolver<double> BounceResolver { get; set; }
    public Resolver<double> FrictionResolver { get; set; }
    public Resolver<double> DragResolver { get; set; }
    public Resolver<double> RadiusResolver { get; set; }
    public bool? IsABox { get; set; }

    /// <summary>
    /// This property holds what the body is tied to, each a term that is a place or a body's name, and how long
    /// the string is, if said.
    /// </summary>
    public List<(Term To, Term Length)> Tethers { get; set; } = [];

    protected override void SetProperties(RenderContext context, Variables variables, Body value)
    {
        value.Mass = MassResolver?.Resolve(context, variables);
        VelocityResolver.AssignTo(value, target => target.Velocity, context, variables);
        BounceResolver.AssignTo(value, target => target.Bounce, context, variables);
        FrictionResolver.AssignTo(value, target => target.Friction, context, variables);
        DragResolver.AssignTo(value, target => target.Drag, context, variables);
        value.Radius = RadiusResolver?.Resolve(context, variables);
        value.IsABox = IsABox;

        if (SpinTerm is not null)
        {
            Vector spin = SpinTerm.GetValue<Vector>(variables);

            value.Spin = context.AnglesAreRadians
                ? spin
                : new Vector(spin.X.ToRadians(), spin.Y.ToRadians(), spin.Z.ToRadians());
        }

        value.Tethers = Tethers
            .Select(tether =>
            {
                object to = tether.To.GetValue(variables);
                double? length = tether.Length?.GetValue<double>(variables);

                return to switch
                {
                    string name => new Tether(null, name, length),
                    NumberTuple place => new Tether(new Point(place.X, place.Y, place.Z), null, length),
                    _ => throw new TokenException("A body is tied to a place, or to another body by its name.")
                    {
                        Token = tether.To.ErrorToken
                    }
                };
            })
            .ToList();
    }
}
